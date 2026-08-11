using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IWorkItemTypeRepository : IRepository<WorkItemType>
{
    /// <summary>Mọi loại của project kèm <c>Fields</c>, sắp theo <c>Order</c> rồi tie-break bằng <c>Id</c>.</summary>
    Task<IReadOnlyList<WorkItemType>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Một loại kèm <c>Fields</c>, CÓ tracking (để sửa/xoá).</summary>
    Task<WorkItemType?> GetWithFieldsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loại mặc định cho task MỚI — loại <c>Order</c> nhỏ nhất.
    /// <para>
    /// Suy từ thứ tự thay vì thêm cờ <c>IsDefault</c>, đúng lý lẽ đã ghi ở
    /// <see cref="IBoardColumnRepository.GetDefaultForProjectAsync"/>: một cờ nữa là một
    /// bất biến nữa phải giữ.
    /// </para>
    /// </summary>
    Task<WorkItemType?> GetDefaultForProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Số task đang mang từng loại — nuôi <c>TaskCount</c> và luồng xoá.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountTasksByTypeAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Chuyển MỌI task từ loại này sang loại kia bằng một lệnh UPDATE hàng loạt.
    /// <para>
    /// ⚠️ Không tải task lên bộ nhớ rồi lặp — cùng lý do
    /// <see cref="IBoardColumnRepository.MoveAllTasksAsync"/>: một loại có thể mang hàng
    /// nghìn task và thao tác chạy trong lúc người dùng đang chờ dialog đóng lại.
    /// </para>
    /// </summary>
    Task<int> MoveAllTasksAsync(Guid fromTypeId, Guid toTypeId, CancellationToken ct = default);

    /// <summary>
    /// Các trường mà loại của MỘT task đang khai, kèm cờ bắt buộc — nuôi cả phần lọc của
    /// <c>GET /tasks/{id}/field-values</c> lẫn phép kiểm required lúc ghi.
    /// </summary>
    Task<IReadOnlyList<WorkItemTypeField>> ListFieldsOfTaskTypeAsync(
        Guid taskId, CancellationToken ct = default);
}
