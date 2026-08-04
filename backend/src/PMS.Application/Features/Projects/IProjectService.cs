using PMS.Application.Common.Models;

namespace PMS.Application.Features.Projects;

public interface IProjectService
{
    Task<ProjectSummaryResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);

    Task<ProjectDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<ProjectSummaryResponse>> GetMineAsync(PagedRequest request, CancellationToken ct = default);

    Task<ProjectDetailResponse> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Đổi trạng thái project (ADR-046, tầng 3). PM-only qua <c>ProjectAction.Update</c>.
    /// <para>
    /// Tách khỏi <see cref="UpdateAsync"/> chứ không thêm một trường vào
    /// <c>UpdateProjectRequest</c>: <c>Status</c> là chuyển TRẠNG THÁI có luật riêng (mở lại
    /// project chưa xong là vô nghĩa), còn Update là ghi đè thông tin mô tả. Gộp lại thì
    /// mỗi lần sửa tên project cũng phải gửi kèm status, và quên gửi là đặt lại trạng thái —
    /// đúng lỗi mà ADR-044 đã trả giá với <c>description</c> của task.
    /// </para>
    /// <para>
    /// Cũng vì vậy hai endpoint này KHÔNG cần <c>RowVersion</c>: chúng chuyển trạng thái chứ
    /// không ghi đè trường nào mà người khác có thể đang sửa song song — cùng lý do
    /// <c>PATCH /tasks/{id}/status</c> không cần (ADR-021).
    /// </para>
    /// </summary>
    Task<ProjectDetailResponse> CompleteAsync(Guid id, CancellationToken ct = default);

    Task<ProjectDetailResponse> ReopenAsync(Guid id, CancellationToken ct = default);
}