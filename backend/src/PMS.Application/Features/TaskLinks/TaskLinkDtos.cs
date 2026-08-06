using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;

namespace PMS.Application.Features.TaskLinks;

public record CreateTaskLinkRequest(Guid TargetTaskId, LinkType LinkType);

/// <summary>
/// Một liên kết nhìn từ phía task đang mở. <c>LinkType</c> ở đây là <b>hướng đã diễn giải
/// cho người xem</b>, không phải giá trị thô trong DB: <c>Blocks(A,B)</c> hiện ra là
/// <c>Blocks</c> khi xem từ A và <c>IsBlockedBy</c> khi xem từ B (ADR-038).
/// </summary>
public record TaskLinkResponse(
    Guid Id,
    LinkType LinkType,
    Guid RelatedTaskId,
    string RelatedTaskCode,
    string RelatedTaskName,
    TaskStatusRef RelatedTaskStatus);
