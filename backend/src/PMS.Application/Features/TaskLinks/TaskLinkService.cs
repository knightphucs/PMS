using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.TaskLinks;

public class TaskLinkService : ITaskLinkService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly ILogger<TaskLinkService> _logger;

    public TaskLinkService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, ILogger<TaskLinkService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaskLinkResponse>> GetByTaskAsync(
        Guid taskId, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        var links = await _uow.TaskLinks.ListByTaskAsync(taskId, ct);
        return links.Select(l => ToResponse(l, taskId)).ToList();
    }

    public async Task<TaskLinkResponse> CreateAsync(
        Guid taskId, CreateTaskLinkRequest request, CancellationToken ct = default)
    {
        // (1) Nạp task nguồn + kiểm quyền TRƯỚC khi chạm tới target: người ngoài project
        //     phải nhận 404 mà không dò được targetTaskId có tồn tại hay không (ADR-019).
        var source = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        await _authz.AuthorizeTaskAsync(source, ProjectAction.ManageTaskLinks, ct);

        // (2) Task đích. 404 nói về ĐÍCH, không phải nguồn.
        var target = await _uow.Tasks.GetByIdAsync(request.TargetTaskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), request.TargetTaskId);

        // (3) Tự liên kết với chính mình
        if (source.Id == target.Id)
            throw new BusinessRuleException("Không thể liên kết một task với chính nó.");

        // (4) Khác project. FK không chặn được điều này vì TaskLink chỉ trỏ tới Task.
        if (source.ProjectId != target.ProjectId)
            throw new BusinessRuleException(
                "Chỉ liên kết được hai task trong cùng một project.");

        // (5) Chuẩn hóa — bước làm cho unique index thực sự kín (ADR-038)
        var (canonicalSource, canonicalTarget, canonicalType) =
            TaskLinkGraph.Canonicalize(source.Id, target.Id, request.LinkType);

        // (6) Trùng. Kiểm trước để có thông điệp rõ ràng; unique index là chốt chặn cuối.
        if (await _uow.TaskLinks.ExistsAsync(canonicalSource, canonicalTarget, canonicalType, ct))
            throw new ConflictException("Hai task này đã có liên kết cùng loại.");

        // (7) Guard chu trình, CHỈ cho Blocks. A chặn B mà B đã (gián tiếp) chặn A thì cả
        //     hai vĩnh viễn không vào được InProgress — livelock nghiệp vụ.
        //     Còn race: hai insert đồng thời vẫn có thể tạo thành chu trình. Chấp nhận có
        //     ý thức — hậu quả là livelock phát hiện được qua EnsureNotBlockedAsync, không
        //     phải crash, và chi phí chặn triệt để (khóa toàn đồ thị) không xứng đáng.
        if (canonicalType == LinkType.Blocks)
        {
            var edges = await _uow.TaskLinks.GetBlockingEdgesAsync(source.ProjectId, ct);
            if (TaskLinkGraph.HasPath(edges, canonicalTarget, canonicalSource))
                throw new ConflictException(
                    "Liên kết này tạo ra vòng chặn: hai task sẽ khóa lẫn nhau và không task nào " +
                    "chuyển sang Đang làm được. Hãy gỡ liên kết chặn ngược lại trước.");
        }

        var link = new TaskLink
        {
            Id = Guid.NewGuid(),
            SourceTaskId = canonicalSource,
            TargetTaskId = canonicalTarget,
            LinkType = canonicalType
        };

        await _uow.TaskLinks.AddAsync(link, ct);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Updated,
            $"Thêm liên kết {request.LinkType} tới task '{target.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tạo liên kết {LinkType} giữa {SourceId} và {TargetId} bởi {EmployeeId}",
            canonicalType, canonicalSource, canonicalTarget, _currentUser.EmployeeId);

        // Nạp lại để có Project của cả hai đầu (cần ghép mã). Một round-trip, chỉ khi tạo.
        var saved = (await _uow.TaskLinks.ListByTaskAsync(taskId, ct))
            .First(l => l.Id == link.Id);

        return ToResponse(saved, taskId);
    }

    public async Task DeleteAsync(Guid linkId, CancellationToken ct = default)
    {
        var link = await _uow.TaskLinks.GetWithTasksAsync(linkId, ct)
            ?? throw new NotFoundException(nameof(TaskLink), linkId);

        // Quyền lấy từ task nguồn — hai đầu luôn cùng project (guard (4) khi tạo).
        await _authz.AuthorizeTaskAsync(link.SourceTask, ProjectAction.ManageTaskLinks, ct);

        _uow.TaskLinks.Remove(link);

        _activityLog.Log(nameof(TaskItem), link.SourceTaskId, ActivityAction.Updated,
            $"Gỡ liên kết {link.LinkType} tới task '{link.TargetTask.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Xóa liên kết {LinkId} bởi {EmployeeId}",
            linkId, _currentUser.EmployeeId);
    }

    /// <summary>
    /// Diễn giải hàng đã chuẩn hóa về góc nhìn của task đang mở — xem
    /// <see cref="TaskLinkGraph.ViewFrom"/>.
    /// </summary>
    private static TaskLinkResponse ToResponse(TaskLink link, Guid viewerTaskId)
    {
        var (displayed, relatedId) = TaskLinkGraph.ViewFrom(
            viewerTaskId, link.SourceTaskId, link.TargetTaskId, link.LinkType);

        var related = relatedId == link.SourceTaskId ? link.SourceTask : link.TargetTask;

        return new TaskLinkResponse(
            link.Id,
            displayed,
            related.Id,
            TaskMapper.FormatCode(related.Project.Key, related.Number),
            related.Name,
            related.Status);
    }
}
