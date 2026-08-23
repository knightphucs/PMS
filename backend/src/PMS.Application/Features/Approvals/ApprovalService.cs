using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Approvals;

/// <summary>
/// Phê duyệt là dữ liệu (ADR-062) — hạng mục đầu tiên cho hệ thống một ĐỘNG TỪ.
///
/// <para>
/// 🔑 <b>Hai mức quyền hoàn toàn khác nhau, và ranh giới giữa chúng là điểm thiết kế chính
/// của lớp này.</b> DỰNG luật đi qua <see cref="ProjectAction.ManageApprovalPolicies"/> (PM,
/// cùng mức cột board / trường / loại việc). KÝ một yêu cầu thì <b>không đi qua
/// <c>RoleInProject</c> chút nào</b> — nó theo <c>ApproverMode</c> của chính luật đó.
/// </para>
/// <para>
/// 🔴 Đây là <b>ngoại lệ có chủ đích thứ hai</b> của mô hình phân quyền hai tầng (thứ nhất là
/// <c>Notification</c>, ADR-023), và nó phải như vậy: người dựng luật và người ký duyệt là
/// hai vai khác nhau: gộp lại thì mọi PM tự ký được luật của chính mình, và cả cơ chế mất
/// nghĩa. Có test riêng cho ranh giới này — nếu không, một phiên sau sẽ "sửa" nó về cho nhất
/// quán và không ai nhận ra gì đã mất.
/// </para>
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, INotificationService notifications,
        ILogger<ApprovalService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _logger = logger;
    }

    // ---------- luật duyệt ----------

    public async Task<IReadOnlyList<ApprovalPolicyResponse>> ListPoliciesAsync(
        Guid projectId, CancellationToken ct = default)
    {
        // View chứ không phải ManageApprovalPolicies: mọi thành viên cần BIẾT luật đang áp
        // lên việc của họ. Giấu luật đi chỉ khiến một thẻ không kéo được trở thành bí ẩn.
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var policies = await _uow.Approvals.ListPoliciesAsync(projectId, ct);
        return policies.Select(ToResponse).ToList();
    }

    public async Task<ApprovalPolicyResponse> CreatePolicyAsync(
        Guid projectId, CreateApprovalPolicyRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageApprovalPolicies, ct);

        var (type, column) = await ResolveTargetsAsync(
            projectId, request.WorkItemTypeId, request.TargetColumnId, ct);

        var approverIds = await ValidateApproversAsync(
            projectId, request.ApproverMode, request.MinApprovals, request.ApproverIds, ct);

        var existing = await _uow.Approvals.FindGateAsync(
            projectId, request.WorkItemTypeId, request.TargetColumnId, ct);

        // Kiểm ở đây thay vì để unique index ném DbUpdateException -> 500. Cùng khuôn
        // SavedViewService/CustomFieldService/BoardColumnService.
        if (existing is not null)
            throw new ConflictException(
                $"Đã có luật duyệt cho '{type.Name}' vào cột '{column.Name}'. Sửa luật đó thay vì tạo thêm.");

        var all = await _uow.Approvals.ListPoliciesAsync(projectId, ct);

        var policy = new ApprovalPolicy
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            WorkItemTypeId = request.WorkItemTypeId,
            // 🔴 Đặt CẢ khoá ngoại lẫn navigation: ToResponse đọc `policy.WorkItemType.Name`
            // ngay sau khi dựng entity trong bộ nhớ, mà EF không nạp navigation hộ cho một
            // entity chưa lưu. Đây đúng cái bẫy đã làm CreateAsync của ADR-060 ném NRE ở MỌI
            // lần tạo task — xem TaskItem.MoveTo làm mẫu.
            WorkItemType = type,
            TargetColumnId = request.TargetColumnId,
            TargetColumn = column,
            ApproverMode = request.ApproverMode,
            MinApprovals = request.MinApprovals,
            Order = all.Count == 0 ? 0 : all.Max(p => p.Order) + 1,
        };

        foreach (var employeeId in approverIds)
            policy.Approvers.Add(new ApprovalPolicyApprover
            {
                ApprovalPolicyId = policy.Id,
                EmployeeId = employeeId,
            });

        _uow.Approvals.AddPolicy(policy);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Created,
            $"Tạo luật duyệt: '{type.Name}' vào cột '{column.Name}' cần {policy.MinApprovals} duyệt");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tạo luật duyệt {PolicyId} cho project {ProjectId}: loại {TypeId} -> cột {ColumnId}",
            policy.Id, projectId, request.WorkItemTypeId, request.TargetColumnId);

        // Đọc lại để có tên approver — danh sách vừa dựng chỉ có id, mà ToResponse cần Name.
        var saved = await _uow.Approvals.GetPolicyWithApproversAsync(policy.Id, ct);
        return ToResponse(saved ?? policy);
    }

    public async Task<ApprovalPolicyResponse> UpdatePolicyAsync(
        Guid id, UpdateApprovalPolicyRequest request, CancellationToken ct = default)
    {
        // CÓ tracking — đường ghi của một quan hệ nhiều-nhiều (xem IApprovalRepository).
        var policy = await _uow.Approvals.GetPolicyWithApproversAsync(id, ct)
            ?? throw new NotFoundException(nameof(ApprovalPolicy), id);

        await _authz.AuthorizeAsync(policy.ProjectId, ProjectAction.ManageApprovalPolicies, ct);

        var (type, column) = await ResolveTargetsAsync(
            policy.ProjectId, request.WorkItemTypeId, request.TargetColumnId, ct);

        var approverIds = await ValidateApproversAsync(
            policy.ProjectId, request.ApproverMode, request.MinApprovals, request.ApproverIds, ct);

        var clash = await _uow.Approvals.FindGateAsync(
            policy.ProjectId, request.WorkItemTypeId, request.TargetColumnId, ct);

        if (clash is not null && clash.Id != policy.Id)
            throw new ConflictException(
                $"Đã có luật duyệt khác cho '{type.Name}' vào cột '{column.Name}'.");

        policy.WorkItemTypeId = request.WorkItemTypeId;
        policy.WorkItemType = type;
        policy.TargetColumnId = request.TargetColumnId;
        policy.TargetColumn = column;
        policy.ApproverMode = request.ApproverMode;
        policy.MinApprovals = request.MinApprovals;

        SyncApprovers(policy, approverIds);

        _activityLog.Log(nameof(Project), policy.ProjectId, ActivityAction.Updated,
            $"Sửa luật duyệt: '{type.Name}' vào cột '{column.Name}' cần {policy.MinApprovals} duyệt");

        await _uow.SaveChangesAsync(ct);

        var saved = await _uow.Approvals.GetPolicyWithApproversAsync(policy.Id, ct);
        return ToResponse(saved ?? policy);
    }

    public async Task DeletePolicyAsync(Guid id, CancellationToken ct = default)
    {
        var policy = await _uow.Approvals.GetPolicyWithApproversAsync(id, ct)
            ?? throw new NotFoundException(nameof(ApprovalPolicy), id);

        await _authz.AuthorizeAsync(policy.ProjectId, ProjectAction.ManageApprovalPolicies, ct);

        var name = $"'{policy.WorkItemType.Name}' vào cột '{policy.TargetColumn.Name}'";

        // Approvals treo dưới ApprovalPolicies bằng Restrict, nên phải dọn trước, nếu không
        // DELETE ném DbUpdateException -> 500. Xem sơ đồ cascade ở ApprovalConfigurations.
        // (ApprovalDecisions và ApprovalPolicyApprovers thì tự đi theo bằng Cascade.)
        await _uow.Approvals.DeleteApprovalsForPolicyAsync(id, ct);
        _uow.Approvals.RemovePolicy(policy);

        _activityLog.Log(nameof(Project), policy.ProjectId, ActivityAction.Deleted,
            $"Xoá luật duyệt: {name}");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Xoá luật duyệt {PolicyId} của project {ProjectId}",
            id, policy.ProjectId);
    }

    // ---------- yêu cầu duyệt ----------

    public async Task<TaskApprovalsResponse> GetForTaskAsync(
        Guid taskId, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        // View: đọc trạng thái duyệt là quyền của MỌI thành viên kể cả Viewer, cùng khuôn
        // ADR-026 đã dùng cho đọc comment và lịch sử.
        await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        var me = _currentUser.RequireEmployeeId();
        var project = await _uow.Projects.GetWithMembersAsync(task.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), task.ProjectId);

        var hasGate = await _uow.Approvals.HasAnyGateForTypeAsync(
            task.ProjectId, task.WorkItemTypeId, ct);

        var all = await _uow.Approvals.ListByTaskAsync(taskId, ct);
        var mapped = all.Select(a => ToResponse(a, me, project)).ToList();

        return new TaskApprovalsResponse(
            HasGate: hasGate,
            Active: mapped.FirstOrDefault(a => a.ConsumedAt is null),
            History: mapped);
    }

    public async Task<ApprovalResponse> DecideAsync(
        Guid approvalId, CreateApprovalDecisionRequest request, CancellationToken ct = default)
    {
        var approval = await _uow.Approvals.GetWithDecisionsAsync(approvalId, ct)
            ?? throw new NotFoundException(nameof(Approval), approvalId);

        // Ngưỡng thấp nhất chỉ để loại người NGOÀI project (404 chứ không 403 — ADR-006).
        // Quyền ký thật thì kiểm ngay bên dưới và KHÔNG đi qua RoleInProject.
        await _authz.AuthorizeTaskAsync(approval.Task, ProjectAction.View, ct);

        var me = _currentUser.RequireEmployeeId();
        var project = await _uow.Projects.GetWithMembersAsync(approval.Task.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), approval.Task.ProjectId);

        var approverIds = ApprovalRules.ResolveApproverIds(approval.ApprovalPolicy, project);

        if (!approverIds.Contains(me))
            throw new ForbiddenException(
                "Bạn không nằm trong danh sách người duyệt của luật này.");

        if (!approval.IsActive)
            throw new ConflictException("Yêu cầu duyệt này đã khép lại.");

        // Bất biến vòng đời + chặn bỏ phiếu hai lần nằm trong entity (DomainException -> 409).
        var decision = approval.AddDecision(
            me, request.Decision, request.Comment, approval.ApprovalPolicy.MinApprovals,
            DateTime.UtcNow);

        var taskName = approval.Task.Name;
        var isReject = request.Decision == DecisionKind.Reject;

        // Ghi TỪNG phiếu chứ không chỉ ghi lúc chốt sổ: câu hỏi kiểm toán là "ai ký", và một
        // dòng duy nhất lúc đủ quorum sẽ giấu mất người ký đầu tiên.
        _activityLog.Log(nameof(TaskItem), approval.TaskId,
            isReject ? ActivityAction.ApprovalRejected : ActivityAction.ApprovalApproved,
            isReject
                ? $"Từ chối yêu cầu duyệt task '{taskName}'"
                  + (decision.Comment is null ? "" : $": {decision.Comment}")
                : $"Duyệt task '{taskName}' ({approval.ApproveCount}/{approval.ApprovalPolicy.MinApprovals})");

        // Báo cho NGƯỜI GỬI, và chỉ khi yêu cầu đã chốt sổ — một phiếu thuận giữa chừng
        // không phải tin tức hành động được. NotifyMany tự loại người thực hiện.
        if (approval.Status is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            _notifications.NotifyMany(
                [approval.RequestedById],
                isReject ? NotificationType.ApprovalRejected : NotificationType.ApprovalApproved,
                isReject
                    ? $"Yêu cầu duyệt task '{taskName}' đã bị từ chối"
                    : $"Task '{taskName}' đã được duyệt — bạn có thể chuyển cột",
                approval.TaskId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Quyết định {Decision} trên approval {ApprovalId} bởi {ActorId}; trạng thái nay là {Status}",
            request.Decision, approvalId, me, approval.Status);

        var reloaded = await _uow.Approvals.GetWithDecisionsAsync(approvalId, ct);
        return ToResponse(reloaded ?? approval, me, project);
    }

    public async Task<ApprovalResponse> CancelAsync(
        Guid approvalId, CancellationToken ct = default)
    {
        var approval = await _uow.Approvals.GetWithDecisionsAsync(approvalId, ct)
            ?? throw new NotFoundException(nameof(Approval), approvalId);

        var role = await _authz.AuthorizeTaskAsync(approval.Task, ProjectAction.View, ct);
        var me = _currentUser.RequireEmployeeId();

        // Luật per-row nên nằm ở service chứ không ở ProjectPermissions — đúng "ranh giới còn
        // lại" của ADR-019, cùng khuôn xoá comment (ADR-026) và xoá đính kèm (ADR-035).
        if (approval.RequestedById != me && role != RoleInProject.ProjectManager)
            throw new ForbiddenException(
                "Chỉ người đã gửi yêu cầu hoặc quản lý dự án mới huỷ được yêu cầu duyệt.");

        approval.Cancel(DateTime.UtcNow);

        _activityLog.Log(nameof(TaskItem), approval.TaskId, ActivityAction.Updated,
            $"Huỷ yêu cầu duyệt task '{approval.Task.Name}'");

        await _uow.SaveChangesAsync(ct);

        var project = await _uow.Projects.GetWithMembersAsync(approval.Task.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), approval.Task.ProjectId);

        return ToResponse(approval, me, project);
    }

    // ---------- phần dùng chung ----------

    /// <summary>
    /// Loại việc và cột đích phải thuộc ĐÚNG project đang cấu hình.
    /// </summary>
    /// <remarks>
    /// 404 chứ không 400 cho id thuộc project khác: người gọi không được biết id đó có tồn
    /// tại ở nơi khác hay không — cùng lý lẽ <c>ProjectAuthorizationService</c> trả 404 cho
    /// người ngoài project (ADR-006).
    /// </remarks>
    private async Task<(WorkItemType Type, BoardColumn Column)> ResolveTargetsAsync(
        Guid projectId, Guid workItemTypeId, Guid targetColumnId, CancellationToken ct)
    {
        var type = await _uow.WorkItemTypes.GetByIdAsync(workItemTypeId, ct);
        if (type is null || type.ProjectId != projectId)
            throw new NotFoundException(nameof(WorkItemType), workItemTypeId);

        var column = await _uow.BoardColumns.GetByIdAsync(targetColumnId, ct);
        if (column is null || column.ProjectId != projectId)
            throw new NotFoundException(nameof(BoardColumn), targetColumnId);

        return (type, column);
    }

    /// <summary>
    /// Danh sách người ký phải là thành viên THẬT của project, và quorum phải với tới được.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> ValidateApproversAsync(
        Guid projectId, ApproverMode mode, int minApprovals,
        IReadOnlyList<Guid>? approverIds, CancellationToken ct)
    {
        if (mode != ApproverMode.NamedApprovers)
            // Danh sách gửi kèm bị BỎ QUA chứ không phải 400: đổi chế độ trên một form đã
            // điền là chuyện thường, bắt người dùng xoá tay danh sách cũ là phiền vô cớ.
            return [];

        var requested = (approverIds ?? []).Distinct().ToList();

        if (requested.Count == 0)
            throw new BusinessRuleException(
                "Chế độ 'người duyệt chỉ định' cần ít nhất một người duyệt.");

        // Lọc ở server: id do client gửi lên, và một id lạ sẽ thành một người ký không bao
        // giờ ký được — cổng khoá chết mà không ai hiểu vì sao. Cùng lý lẽ bộ lọc @mention
        // (ADR-048), chỉ khác là ở đây im lặng bỏ qua sẽ nguy hiểm nên ta báo lỗi.
        var valid = await _uow.ProjectMembers.FilterActiveMemberIdsAsync(projectId, requested, ct);

        if (valid.Count != requested.Count)
            throw new BusinessRuleException(
                "Một hoặc nhiều người duyệt không phải thành viên đang hoạt động của dự án.");

        // 🔴 Quorum lớn hơn số người ký = cổng KHÔNG BAO GIỜ mở được. Đúng luật 4 của Doctrine
        // (§0): không ship một cấu hình mà người dùng lưu được nhưng không dùng được.
        if (minApprovals > valid.Count)
            throw new BusinessRuleException(
                $"Cần {minApprovals} lượt duyệt nhưng chỉ có {valid.Count} người duyệt — cổng sẽ không bao giờ mở được.");

        return valid;
    }

    /// <summary>
    /// Đồng bộ danh sách người ký mà KHÔNG xoá-rồi-thêm-lại toàn bộ.
    /// </summary>
    /// <remarks>
    /// Xoá trắng rồi thêm lại trong cùng một <c>SaveChanges</c> khiến EF sinh DELETE và INSERT
    /// cho cùng một khoá chính ghép, và thứ tự hai lệnh đó không được đảm bảo →
    /// <c>Violation of PRIMARY KEY constraint</c>. Chỉ chạm vào phần thật sự đổi.
    /// </remarks>
    private static void SyncApprovers(ApprovalPolicy policy, IReadOnlyList<Guid> approverIds)
    {
        var target = approverIds.ToHashSet();

        foreach (var gone in policy.Approvers.Where(a => !target.Contains(a.EmployeeId)).ToList())
            policy.Approvers.Remove(gone);

        var present = policy.Approvers.Select(a => a.EmployeeId).ToHashSet();

        foreach (var added in target.Where(id => !present.Contains(id)))
            policy.Approvers.Add(new ApprovalPolicyApprover
            {
                ApprovalPolicyId = policy.Id,
                EmployeeId = added,
            });
    }

    private static ApprovalPolicyResponse ToResponse(ApprovalPolicy policy)
        => new(
            policy.Id,
            policy.ProjectId,
            policy.WorkItemTypeId,
            policy.WorkItemType.Name,
            policy.TargetColumnId,
            policy.TargetColumn.Name,
            policy.ApproverMode,
            policy.MinApprovals,
            policy.Order,
            policy.Approvers
                  .Select(a => new ApproverDto(a.EmployeeId, a.Employee?.Name ?? string.Empty))
                  .ToList());

    private static ApprovalResponse ToResponse(Approval approval, Guid me, Project project)
    {
        var policy = approval.ApprovalPolicy;
        var approverIds = ApprovalRules.ResolveApproverIds(policy, project);

        var canDecide = approval.IsActive
                     && approval.Status == ApprovalStatus.Pending
                     && approverIds.Contains(me)
                     && approval.Decisions.All(d => d.ApproverId != me);

        var isManager = project.Members.Any(
            m => m.EmployeeId == me && m.RoleInProject == RoleInProject.ProjectManager && m.IsActive());

        return new ApprovalResponse(
            approval.Id,
            approval.TaskId,
            approval.ApprovalPolicyId,
            policy.TargetColumn?.Name ?? string.Empty,
            approval.Status,
            approval.RequestedById,
            approval.RequestedBy?.Name ?? string.Empty,
            approval.RequestedAt,
            approval.DecidedAt,
            approval.ConsumedAt,
            approval.ApproveCount,
            policy.MinApprovals,
            approval.Decisions
                    .OrderBy(d => d.DecidedAt)
                    .Select(d => new ApprovalDecisionDto(
                        d.Id, d.ApproverId, d.Approver?.Name ?? string.Empty,
                        d.Decision, d.Comment, d.DecidedAt))
                    .ToList(),
            CanDecide: canDecide,
            CanCancel: approval.IsActive && (approval.RequestedById == me || isManager));
    }
}
