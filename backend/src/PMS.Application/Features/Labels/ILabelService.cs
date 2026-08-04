namespace PMS.Application.Features.Labels;

public interface ILabelService
{
    Task<IReadOnlyList<LabelResponse>> GetAllAsync(CancellationToken ct = default);
    Task<LabelResponse> CreateAsync(CreateLabelRequest request, CancellationToken ct = default);

    /// <summary>Sửa nhãn toàn cục — chỉ SystemAdmin, gác bằng policy ở controller (ADR-037).</summary>
    Task<LabelResponse> UpdateAsync(Guid id, UpdateLabelRequest request, CancellationToken ct = default);

    /// <summary>Xóa nhãn toàn cục — chỉ SystemAdmin. Gỡ chip khỏi MỌI task đang gắn nhãn đó.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<LabelResponse>> AttachToTaskAsync(Guid taskId, Guid labelId, CancellationToken ct = default);
    Task<IReadOnlyList<LabelResponse>> DetachFromTaskAsync(Guid taskId, Guid labelId, CancellationToken ct = default);
}
