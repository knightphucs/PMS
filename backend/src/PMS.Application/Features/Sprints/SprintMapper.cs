using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Sprints;

/// <summary>
/// ⚠️ Viết TAY chứ không để Mapperly sinh, kể từ ADR-050.
///
/// <para>
/// <c>DoneCount</c> phải đếm task thuộc cột nhóm <c>Done</c>, mà Mapperly chỉ ghép được
/// property cùng tên — nó không biểu diễn được một phép đếm có điều kiện. Để nó tự sinh thì
/// trường này sẽ im lặng ra 0 ở mọi sprint, đúng lớp lỗi mà <c>SubtaskProgress</c> đã trả
/// giá một lần (luôn trả 0 vì ba query board/backlog thiếu Include).
/// </para>
/// <para>
/// 🔴 Kéo theo một ràng buộc: mọi query nuôi mapper này phải <c>Include(s =&gt; s.Tasks)</c>.
/// <c>SprintRepository.GetByProjectAsync</c> và <c>GetWithTasksAsync</c> đã có sẵn.
/// </para>
/// </summary>
public class SprintMapper
{
    public SprintResponse ToResponse(Sprint sprint) => new(
        sprint.Id,
        sprint.ProjectId,
        sprint.Name,
        sprint.Goal,
        sprint.StartDate,
        sprint.EndDate,
        sprint.IsActive,
        sprint.Tasks.Count,
        sprint.Status,
        sprint.CompletedAt,
        sprint.Tasks.Count(t => t.Category == StatusCategory.Done));
}
