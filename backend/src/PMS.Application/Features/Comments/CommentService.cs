using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Comments;

public class CommentService : ICommentService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly CommentMapper _mapper;
    private readonly ILogger<CommentService> _logger;

    public CommentService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, INotificationService notifications,
        CommentMapper mapper, ILogger<CommentService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<CommentResponse> CreateAsync(
        Guid taskId, CreateCommentRequest request, CancellationToken ct = default)
    {
        var authorId = _currentUser.RequireEmployeeId();

        // GetWithNotificationTargetsAsync: cần Assignments + Watchers để biết gửi thông báo
        // cho ai. Collection chưa nạp thì rỗng và thông báo âm thầm gửi thiếu người.
        var task = await _uow.Tasks.GetWithNotificationTargetsAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        // CreateComment chặn Viewer; AuthorizeTaskAsync chuẩn hóa 404 theo ADR-019.
        await _authz.AuthorizeTaskAsync(task, ProjectAction.CreateComment, ct);

        var comment = new Comment
        {
            // Id sinh phía app: ApplyIdNeverGenerated() tắt sinh Id ở DB nên để mặc định
            // Guid.Empty thì bản ghi thứ hai vi phạm khóa chính (bài học 2026-07-30, điểm 1).
            Id = Guid.NewGuid(),
            TaskId = taskId,
            EmployeeId = authorId,
            Content = request.Content.Trim()
        };

        _uow.Comments.Add(comment);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Commented,
            $"Bình luận trên task '{task.Name}'");

        // Cùng danh sách người nhận với luồng đổi trạng thái — dùng lại
        // TaskNotificationExtensions thay vì viết bản sao thứ hai sẽ lệch dần.
        _notifications.NotifyMany(task.InterestedEmployeeIds(), NotificationType.CommentAdded,
            $"Có bình luận mới trên task '{task.Name}'", taskId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Thêm comment {CommentId} trên task {TaskId} bởi {AuthorId}",
            comment.Id, taskId, authorId);

        // Navigation Author còn null (comment vừa tạo trong bộ nhớ) nên mapper đọc
        // Author.Name sẽ NRE. Gán SAU SaveChanges để EF không hiểu nhầm là muốn insert
        // Employee mới — đúng cách ProjectMemberService.InviteAsync đã làm.
        comment.Author = await _uow.Employees.GetByIdAsync(authorId, ct)
            ?? throw new NotFoundException(nameof(Employee), authorId);

        return _mapper.ToResponse(comment);
    }

    public async Task<PagedResult<CommentResponse>> GetByTaskAsync(
        Guid taskId, PagedRequest request, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        // Chỉ cần View: Viewer đọc được thảo luận, chỉ không viết được (§10).
        await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        var paged = await _uow.Comments.GetPagedByTaskAsync(taskId, request, ct);

        return paged.Map(_mapper.ToResponse);
    }

    public async Task<CommentResponse> UpdateAsync(
        Guid id, UpdateCommentRequest request, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();
        var (comment, _) = await LoadAndAuthorizeAsync(id, ct);

        // ADR-026: sửa CHỈ tác giả — kể cả ProjectManager cũng không. Xóa lời người khác là
        // hành vi kiểm duyệt hợp lý của PM; viết lại lời người khác thì không, vì nội dung
        // vẫn đứng tên tác giả cũ.
        if (comment.EmployeeId != actorId)
            throw new ForbiddenException("Chỉ tác giả mới sửa được bình luận của mình.");

        comment.Content = request.Content.Trim();

        _activityLog.Log(nameof(TaskItem), comment.TaskId, ActivityAction.CommentUpdated,
            $"Sửa bình luận trên task '{comment.Task.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Sửa comment {CommentId} bởi {ActorId}", id, actorId);

        return _mapper.ToResponse(comment);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();
        var (comment, role) = await LoadAndAuthorizeAsync(id, ct);

        var isAuthor = comment.EmployeeId == actorId;
        if (!isAuthor && role != RoleInProject.ProjectManager)
            throw new ForbiddenException(
                "Chỉ tác giả hoặc ProjectManager của project mới xóa được bình luận này.");

        // ADR-026: xóa CỨNG. Comment không phải ISoftDeletable — nhất quán ADR-012 (gỡ
        // member cũng là xóa cứng): thêm cờ đã-xóa thì mọi query comment sau này phải nhớ
        // lọc thêm một điều kiện, đúng lớp lỗi mà việc thiếu ISoftDeletable từng gây ra.
        // Audit trail do ActivityLog đảm nhiệm.
        _uow.Comments.Remove(comment);

        _activityLog.Log(nameof(TaskItem), comment.TaskId, ActivityAction.CommentDeleted,
            isAuthor
                ? $"Xóa bình luận của chính mình trên task '{comment.Task.Name}'"
                : $"ProjectManager xóa bình luận của {comment.Author.Name} trên task '{comment.Task.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Xóa comment {CommentId} bởi {ActorId} (isAuthor={IsAuthor})",
            id, actorId, isAuthor);
    }

    /// <summary>
    /// Nạp comment rồi kiểm quyền theo project chứa task của nó, trả về cả
    /// <c>RoleInProject</c> để caller áp luật per-row (tác giả vs PM).
    /// <para>
    /// Ngưỡng dùng ở đây là <c>View</c>, không phải một action ghi: mục đích chỉ là loại
    /// người ngoài project (404) và lấy được role — luật thật nằm ở caller, đúng khuôn
    /// <c>TaskStatusTransitionService.EnsureCanChangeStatus</c> (ADR-017).
    /// </para>
    /// <para>
    /// Chuẩn hóa 404 (ADR-019): comment không tồn tại và comment thuộc project mình không
    /// phải thành viên đều trả cùng một thông báo về Comment, không để lộ sự tồn tại của
    /// bản ghi cho người ngoài.
    /// </para>
    /// </summary>
    private async Task<(Comment Comment, RoleInProject Role)> LoadAndAuthorizeAsync(
        Guid id, CancellationToken ct)
    {
        var comment = await _uow.Comments.GetWithTaskAsync(id, ct)
            ?? throw new NotFoundException(nameof(Comment), id);

        try
        {
            var role = await _authz.AuthorizeAsync(comment.Task.ProjectId, ProjectAction.View, ct);
            return (comment, role);
        }
        catch (NotFoundException)
        {
            throw new NotFoundException(nameof(Comment), id);
        }
    }
}
