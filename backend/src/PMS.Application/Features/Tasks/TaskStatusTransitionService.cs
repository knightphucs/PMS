using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Approvals;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public class TaskStatusTransitionService : ITaskStatusTransitionService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly TaskMapper _mapper;
    private readonly ILogger<TaskStatusTransitionService> _logger;

    public TaskStatusTransitionService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, INotificationService notifications,
        TaskMapper mapper, ILogger<TaskStatusTransitionService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<TaskSummaryResponse> ChangeStatusAsync(
        Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();

        var task = await _uow.Tasks.GetForStatusChangeAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        // View là ngưỡng thấp nhất: chỉ để loại người ngoài project (404)
        var role = await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        EnsureCanChangeStatus(task, role, actorId);

        var target = await _uow.BoardColumns.GetByIdAsync(request.TargetColumnId, ct)
            ?? throw new NotFoundException(nameof(BoardColumn), request.TargetColumnId);

        // Cột của project khác -> DomainException (409) từ chính entity. Kiểm ở domain chứ
        // không ở đây vì đó là bất biến của TaskItem, không phải luật của riêng use case này.
        if (target.ProjectId != task.ProjectId)
            throw new NotFoundException(nameof(BoardColumn), request.TargetColumnId);

        // Kéo về đúng cột đang đứng: không còn là lỗi 409 như thời enum (ADR-052 thay ADR-021).
        // Với cột do người dùng tạo thì "đứng yên" chỉ là một thao tác thừa, không phải một
        // bước đi không hợp lệ — và frontend vốn đã chặn nó bằng cấu trúc.
        if (task.BoardColumnId == target.Id)
            return _mapper.ToSummary(task, await RequireProjectKeyAsync(task.ProjectId, ct));

        // Guard duy nhất còn sót lại của state machine cũ: không bắt đầu một task đang bị chặn.
        // ⚠️ Điều kiện đổi từ "target == InProgress" sang "NHÓM của cột đích là InProgress",
        // nên một cột tự đặt tên "Chờ QA" thuộc nhóm InProgress cũng được bảo vệ. Hệ quả:
        // cột "Đang duyệt" mặc định nay cũng bị kiểm, trước đây thì không — đúng hơn về
        // nghiệp vụ (đẩy task bị chặn đi tiếp vẫn là đẩy task bị chặn).
        if (target.Category == StatusCategory.InProgress)
            await EnsureNotBlockedAsync(taskId, ct);

        // Guard thứ hai, và là ĐỘNG TỪ đầu tiên của hệ thống (ADR-062). Đứng ở đây — sau chốt
        // no-op cùng cột, trước MoveTo — là có chủ đích: kéo về đúng cột đang đứng không được
        // sinh ra một yêu cầu duyệt.
        await EnsureApprovedAsync(task, target, actorId, ct);

        var previous = task.BoardColumn.Name;

        task.MoveTo(target);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.StatusChanged,
            $"Đổi trạng thái task '{task.Name}': {previous} -> {target.Name}");

        _notifications.NotifyMany(task.InterestedEmployeeIds(), NotificationType.StatusChanged,
            $"Task '{task.Name}' đã chuyển từ {previous} sang {target.Name}", taskId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Đổi trạng thái task {TaskId}: {Previous} -> {Target} bởi {ActorId} (role {Role})",
            taskId, previous, target.Name, actorId, role);

        return _mapper.ToSummary(task, await RequireProjectKeyAsync(task.ProjectId, ct));
    }

    private async Task<string> RequireProjectKeyAsync(Guid projectId, CancellationToken ct)
        => await _uow.Projects.GetKeyAsync(projectId, ct)
           ?? throw new NotFoundException(nameof(Project), projectId);

    /// <summary>
    /// ADR-017: Assignee của chính task đó HOẶC ProjectManager của project chứa task.
    /// Member không phải assignee và Viewer đều bị từ chối.
    /// </summary>
    private static void EnsureCanChangeStatus(TaskItem task, RoleInProject role, Guid actorId)
    {
        if (role == RoleInProject.ProjectManager) return;
        if (task.Assignments.Any(a => a.EmployeeId == actorId)) return;

        throw new ForbiddenException(
            "Chỉ người được gán task hoặc ProjectManager của project mới được đổi trạng thái task này.");
    }

    /// <summary>
    /// Task bị chặn khi có TaskLink Blocks/IsBlockedBy trỏ tới một task chưa Done (§5).
    /// Chỉ kiểm khi chuyển sang InProgress — lùi về ToDo hay đóng task thì không cần.
    /// </summary>
    private async Task EnsureNotBlockedAsync(Guid taskId, CancellationToken ct)
    {
        var blockers = await _uow.Tasks.GetUnfinishedBlockersAsync(taskId, ct);
        if (blockers.Count == 0) return;

        var names = string.Join(", ", blockers.Select(b => $"'{b.Name}'"));
        throw new ConflictException(
            $"Không thể bắt đầu task khi còn bị chặn bởi {blockers.Count} task chưa hoàn thành: {names}.");
    }

    /// <summary>
    /// Cổng duyệt (ADR-062): "task thuộc loại X muốn vào cột Y thì cần N người ký".
    ///
    /// <para>
    /// 🔴 <b>Đây là điểm cưỡng chế DUY NHẤT của cả cơ chế phê duyệt.</b> Nếu bạn đang định
    /// thêm một phép kiểm duyệt ở chỗ khác, dừng lại: hai điểm cưỡng chế nghĩa là một trong
    /// hai sẽ lệch, và cái lệch sẽ là cái không ai test.
    /// </para>
    /// <para>
    /// ⚠️ Phương thức này <b>có tác dụng phụ hợp lệ</b>, khác hẳn <see cref="EnsureNotBlockedAsync"/>
    /// vốn thuần đọc: lần kéo đầu tiên vào một cột có cổng sẽ SINH ra yêu cầu duyệt rồi mới
    /// ném 409. Đó là quyết định (a) của ADR-062 — không có nút "Gửi duyệt" nào cả, vì thêm
    /// nút là thêm một bề mặt lên màn làm việc (luật 5 Doctrine) và bắt người dùng phải
    /// <i>biết trước</i> rằng loại việc này có cổng.
    /// </para>
    /// <para>
    /// Hệ quả frontend phải xử lý: 409 ở lần kéo đầu <b>không phải một lỗi</b> — hiển thị nó
    /// thành một toast đỏ trơn là nói dối người dùng về thứ vừa xảy ra.
    /// </para>
    /// </summary>
    private async Task EnsureApprovedAsync(
        TaskItem task, BoardColumn target, Guid actorId, CancellationToken ct)
    {
        // Truy vấn trên đường nóng: chạy ở MỌI lần đổi trạng thái của mọi task, kể cả ở
        // project chưa từng khai luật nào. Đi thẳng vào unique index, không Include thừa.
        var policy = await _uow.Approvals.FindGateAsync(
            task.ProjectId, task.WorkItemTypeId, target.Id, ct);

        // Không có cổng: đường cũ nguyên vẹn, không một truy vấn nào thêm.
        if (policy is null) return;

        var approval = await _uow.Approvals.GetActiveAsync(task.Id, policy.Id, ct);

        if (approval is { Status: ApprovalStatus.Approved })
        {
            // Tiêu thụ tại đây, KHÔNG đổi Status: hàng ở nguyên `Approved` làm lịch sử kiểm
            // toán. Đây là chỗ quyết định "rời cột rồi quay lại phải duyệt LẠI" được cài đặt
            // — lần sau GetActiveAsync sẽ không thấy hàng này nữa (ADR-062 quyết định b).
            approval.Consume(DateTime.UtcNow);
            return;
        }

        if (approval is { Status: ApprovalStatus.Rejected })
        {
            var last = approval.Decisions
                               .Where(d => d.Decision == DecisionKind.Reject)
                               .OrderByDescending(d => d.DecidedAt)
                               .FirstOrDefault();

            var who = last?.Approver?.Name ?? "người duyệt";
            var why = string.IsNullOrWhiteSpace(last?.Comment) ? "" : $": {last!.Comment}";

            throw new ConflictException(
                $"Yêu cầu duyệt để vào '{target.Name}' đã bị {who} từ chối{why}. "
                + "Huỷ yêu cầu này rồi gửi lại nếu muốn thử lần nữa.")
            { Code = ApprovalCodes.Rejected };
        }

        if (approval is { Status: ApprovalStatus.Pending })
            // 🔴 KHÔNG sinh hàng mới. Nếu sinh, mỗi cú kéo lại là một yêu cầu nữa và hộp thư
            // của người duyệt thành bãi rác — đúng thứ khiến người ta tắt thông báo.
            throw new ConflictException(
                $"Task đang chờ duyệt để vào '{target.Name}' — đã có "
                + $"{approval.ApproveCount}/{policy.MinApprovals} lượt duyệt.")
            { Code = ApprovalCodes.Pending };

        await CreateApprovalRequestAsync(task, target, policy, actorId, ct);
    }

    /// <summary>Sinh yêu cầu duyệt và báo cho những người ký được.</summary>
    private async Task CreateApprovalRequestAsync(
        TaskItem task, BoardColumn target, ApprovalPolicy policy, Guid actorId, CancellationToken ct)
    {
        var project = await _uow.Projects.GetWithMembersAsync(task.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), task.ProjectId);

        // Dùng chung với ApprovalService.DecideAsync — chép luật này ra hai chỗ nghĩa là hệ
        // thống báo cho một nhóm rồi từ chối đúng nhóm đó (ADR-034).
        var approverIds = ApprovalRules.ResolveApproverIds(policy, project);

        if (approverIds.Count == 0)
            throw new ConflictException(
                $"Cột '{target.Name}' cần được duyệt nhưng luật duyệt không còn người duyệt nào. "
                + "Nhờ quản lý dự án sửa lại luật ở trang Cấu hình.");

        var approval = new Approval
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            ApprovalPolicyId = policy.Id,
            Status = ApprovalStatus.Pending,
            RequestedById = actorId,
            RequestedAt = DateTime.UtcNow,
        };

        await _uow.Approvals.AddAsync(approval, ct);

        _activityLog.Log(nameof(TaskItem), task.Id, ActivityAction.ApprovalRequested,
            $"Gửi yêu cầu duyệt task '{task.Name}' để vào cột '{target.Name}'");

        _notifications.NotifyMany(approverIds, NotificationType.ApprovalRequested,
            $"Task '{task.Name}' cần bạn duyệt để vào '{target.Name}'", task.Id);

        // 🔴 Lưu TRƯỚC khi ném, nếu không tác dụng phụ biến mất cùng ngoại lệ và người dùng
        // kéo mãi mà không có yêu cầu nào được gửi đi.
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Sinh yêu cầu duyệt {ApprovalId} cho task {TaskId} vào cột {ColumnId}; {Count} người duyệt",
            approval.Id, task.Id, target.Id, approverIds.Count);

        var quorum = policy.MinApprovals == 1
            ? "1 lượt duyệt"
            : $"{policy.MinApprovals} lượt duyệt";

        // 🔑 Mã RIÊNG cho nhánh này: đây là 409 duy nhất trong hệ thống mà thao tác của người
        // dùng đã THÀNH CÔNG một nửa. Client hiện nó thành thông báo trung tính, không phải
        // toast đỏ — xem ApprovalCodes.
        throw new ConflictException(
            $"Cột '{target.Name}' cần được duyệt. Đã gửi yêu cầu tới {approverIds.Count} người duyệt "
            + $"— cần {quorum} để chuyển được.")
        { Code = ApprovalCodes.Requested };
    }
}
