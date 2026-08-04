using PMS.Domain.Enums;

namespace PMS.Application.Features.ActivityLogs;

public record ActivityLogResponse(
    Guid Id,
    ActivityAction Action,
    string Detail,
    Guid ActorId,
    string ActorName,
    DateTime CreatedAt);

/// <summary>
/// Bản dành cho nhật ký cấp hệ thống — thêm <c>EntityType</c>/<c>EntityId</c> vì màn admin
/// gom nhiều loại đối tượng vào một dòng thời gian, khác feed của một task/project cụ thể.
/// </summary>
public record SystemAuditLogResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    ActivityAction Action,
    string Detail,
    Guid ActorId,
    string ActorName,
    DateTime CreatedAt);
