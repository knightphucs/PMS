using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Attachments;

public class AttachmentService : IAttachmentService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly IFileStorage _storage;
    private readonly IAttachmentPolicy _policy;
    private readonly AttachmentMapper _mapper;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, IFileStorage storage, IAttachmentPolicy policy,
        AttachmentMapper mapper, ILogger<AttachmentService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _storage = storage;
        _policy = policy;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<AttachmentResponse> UploadToTaskAsync(
        Guid taskId, UploadAttachmentRequest request, CancellationToken ct = default)
    {
        // (1) QUYỀN TRƯỚC TIÊN — trước khi đọc dù chỉ một byte. Người không được phép tải
        //     lên thì không được làm server tiêu tốn I/O, và cũng không được biết taskId
        //     có tồn tại hay không (AuthorizeTaskAsync chuẩn hóa 404 — ADR-019).
        var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.UploadAttachment, ct);

        var attachment = await StoreAsync(
            request,
            (stored, contentType) => Attachment.ForTask(
                taskId, _currentUser.RequireEmployeeId(),
                request.FileName, stored, contentType, request.SizeBytes),
            ct);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Updated,
            $"Đính kèm file '{request.FileName}' vào task '{task.Name}'");

        await SaveOrRollbackAsync(attachment, ct);

        return await LoadResponseAsync(attachment.Id, ct);
    }

    public async Task<AttachmentResponse> UploadToProjectAsync(
        Guid projectId, UploadAttachmentRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.UploadAttachment, ct);

        var attachment = await StoreAsync(
            request,
            (stored, contentType) => Attachment.ForProject(
                projectId, _currentUser.RequireEmployeeId(),
                request.FileName, stored, contentType, request.SizeBytes),
            ct);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Updated,
            $"Đính kèm file '{request.FileName}' vào project");

        await SaveOrRollbackAsync(attachment, ct);

        return await LoadResponseAsync(attachment.Id, ct);
    }

    public async Task<IReadOnlyList<AttachmentResponse>> GetByTaskAsync(
        Guid taskId, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        // Đọc = View: Viewer cũng xem và tải được, đúng khuôn ADR-026 cho comment.
        await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        return (await _uow.Attachments.ListByTaskAsync(taskId, ct))
            .Select(_mapper.ToResponse).ToList();
    }

    public async Task<IReadOnlyList<AttachmentResponse>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        return (await _uow.Attachments.ListByProjectAsync(projectId, ct))
            .Select(_mapper.ToResponse).ToList();
    }

    public async Task<AttachmentDownload> DownloadAsync(
        Guid attachmentId, CancellationToken ct = default)
    {
        var (attachment, _) = await LoadAndAuthorizeAsync(attachmentId, ProjectAction.View, ct);

        // Đường dẫn KHÔNG dựng từ đầu vào người dùng: StoredFileName do IFileStorage sinh
        // và LocalFileStorage vẫn tự kiểm containment thêm một lần nữa.
        var stream = await _storage.OpenReadAsync(attachment.StoredFileName, ct);

        return new AttachmentDownload(stream, attachment.FileName);
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct = default)
    {
        // Authorize bằng View để lấy vai trò và loại người ngoài project (404), rồi tự áp
        // luật per-row — đúng khuôn CommentService.DeleteAsync của ADR-026.
        var (attachment, role) = await LoadAndAuthorizeAsync(attachmentId, ProjectAction.View, ct);

        var actorId = _currentUser.RequireEmployeeId();
        var isUploader = attachment.UploaderId == actorId;

        if (!isUploader && role != RoleInProject.ProjectManager)
            throw new ForbiddenException(
                "Chỉ người đã tải file lên hoặc Project Manager mới được xóa file đính kèm.");

        var storedFileName = attachment.StoredFileName;
        _uow.Attachments.Remove(attachment);

        _activityLog.Log(
            attachment.TaskId is not null ? nameof(TaskItem) : nameof(Project),
            attachment.TaskId ?? attachment.ProjectId!.Value,
            ActivityAction.Updated,
            $"Gỡ file đính kèm '{attachment.FileName}'");

        await _uow.SaveChangesAsync(ct);

        // Xóa file SAU khi commit hàng DB. Ngược lại thì SaveChanges hỏng sẽ để lại một
        // hàng trỏ tới file đã biến mất — tệ hơn hẳn một file mồ côi trên đĩa.
        await _storage.DeleteAsync(storedFileName, ct);

        _logger.LogInformation("Xóa file đính kèm {AttachmentId} bởi {EmployeeId}",
            attachmentId, actorId);
    }

    /// <summary>
    /// Chạy đủ chuỗi kiểm tra rồi ghi file xuống storage. Trả về entity <b>chưa</b> lưu DB.
    /// </summary>
    private async Task<Attachment> StoreAsync(
        UploadAttachmentRequest request,
        Func<string, string, Attachment> factory,
        CancellationToken ct)
    {
        // (2)-(7) metadata
        var extension = AttachmentContentValidator.ValidateMetadata(
            request.FileName, request.ContentType, request.SizeBytes, _policy);

        // (8) chữ ký — bước duy nhất nhìn vào nội dung
        var header = new byte[AttachmentContentValidator.SignatureBufferSize];
        var read = await ReadHeaderAsync(request.Content, header, ct);
        AttachmentContentValidator.ValidateSignature(header.AsSpan(0, read), extension);

        // Tua lại: ValidateSignature đã tiêu thụ mất phần đầu, không tua thì file lưu xuống
        // sẽ THIẾU 8 byte đầu — hỏng im lặng, chỉ lộ khi ai đó mở file ra xem.
        if (request.Content.CanSeek) request.Content.Seek(0, SeekOrigin.Begin);

        var storedFileName = await _storage.SaveAsync(request.Content, extension, ct);

        return factory(storedFileName, request.ContentType);
    }

    /// <summary>
    /// Một <c>ReadAsync</c> không bảo đảm lấy đủ số byte yêu cầu (stream mạng trả về từng
    /// phần). Phải lặp cho tới khi đủ hoặc hết file, nếu không file hợp lệ sẽ ngẫu nhiên bị
    /// từ chối vì đọc hụt chữ ký.
    /// </summary>
    private static async Task<int> ReadHeaderAsync(Stream source, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await source.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    /// <summary>
    /// Lưu hàng DB; hỏng thì dọn file vừa ghi rồi ném tiếp. File mồ côi sau khi tiến trình
    /// chết đột ngột là chấp nhận được (không gì phục vụ thư mục đó), nhưng để lại rác vì
    /// một lỗi đã bắt được thì không.
    /// </summary>
    private async Task SaveOrRollbackAsync(Attachment attachment, CancellationToken ct)
    {
        await _uow.Attachments.AddAsync(attachment, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            await _storage.DeleteAsync(attachment.StoredFileName, CancellationToken.None);
            throw;
        }

        _logger.LogInformation(
            "Tải lên file {FileName} ({SizeBytes} byte) -> {AttachmentId} bởi {EmployeeId}",
            attachment.FileName, attachment.SizeBytes, attachment.Id, _currentUser.EmployeeId);
    }

    private async Task<AttachmentResponse> LoadResponseAsync(Guid id, CancellationToken ct)
    {
        // Nạp lại để có Uploader.Name — response cần tên người tải lên.
        var saved = await _uow.Attachments.GetWithUploaderAsync(id, ct)
            ?? throw new NotFoundException(nameof(Attachment), id);

        return _mapper.ToResponse(saved);
    }

    /// <summary>
    /// Nạp attachment rồi kiểm quyền theo project của chủ sở hữu — dù chủ là Task hay
    /// Project. Trả kèm vai trò để caller áp luật per-row.
    /// </summary>
    private async Task<(Attachment Attachment, RoleInProject Role)> LoadAndAuthorizeAsync(
        Guid attachmentId, ProjectAction action, CancellationToken ct)
    {
        var attachment = await _uow.Attachments.GetWithUploaderAsync(attachmentId, ct)
            ?? throw new NotFoundException(nameof(Attachment), attachmentId);

        Guid projectId;
        if (attachment.TaskId is { } taskId)
        {
            var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
                ?? throw new NotFoundException(nameof(Attachment), attachmentId);
            projectId = task.ProjectId;
        }
        else
        {
            projectId = attachment.ProjectId!.Value;   // CHECK constraint bảo đảm đúng một chủ
        }

        try
        {
            var role = await _authz.AuthorizeAsync(projectId, action, ct);
            return (attachment, role);
        }
        catch (NotFoundException)
        {
            // Chuẩn hóa 404 về ATTACHMENT: nếu để nguyên 404 về Project thì người ngoài
            // phân biệt được "attachment không tồn tại" với "tồn tại nhưng ở project tôi
            // không thuộc" — đủ để dò (ADR-019).
            throw new NotFoundException(nameof(Attachment), attachmentId);
        }
    }
}
