namespace PMS.Application.Features.Sprints;

public record CreateSprintRequest(string Name, string Goal, DateTime StartDate, DateTime EndDate);

public record UpdateSprintRequest(string Name, string Goal, DateTime StartDate, DateTime EndDate);

/// <summary>
/// Sprint chỉ có một dạng response (không tách Summary/Detail như Project) vì entity nhỏ,
/// hai bản sẽ trùng nhau hoàn toàn. Danh sách task của sprint lấy qua endpoint Board —
/// đó mới là cách nhìn đúng của một sprint.
/// </summary>
public record SprintResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Goal,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    int TaskCount);
