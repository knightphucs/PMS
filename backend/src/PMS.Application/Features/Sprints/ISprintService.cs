namespace PMS.Application.Features.Sprints;

public interface ISprintService
{
    Task<SprintResponse> CreateAsync(
        Guid projectId, CreateSprintRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<SprintResponse>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default);

    Task<SprintResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<SprintResponse> UpdateAsync(
        Guid id, UpdateSprintRequest request, CancellationToken ct = default);

    /// <summary>Xóa mềm sprint và đẩy toàn bộ task của nó về Backlog (ADR-020).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Bắt đầu sprint. Một project chỉ có tối đa MỘT sprint đang chạy (ADR-050).</summary>
    Task<SprintResponse> StartAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Xem trước việc đóng sprint — bao nhiêu task chưa xong, và đẩy sang được những sprint
    /// nào. Frontend gọi trước khi mở dialog đóng sprint (ADR-050).
    /// </summary>
    Task<SprintCompletionPreview> PreviewCompletionAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Đóng sprint và chuyển task chưa xong theo lựa chọn của người dùng (ADR-050).
    /// </summary>
    Task<SprintResponse> CompleteAsync(
        Guid id, CompleteSprintRequest request, CancellationToken ct = default);
}
