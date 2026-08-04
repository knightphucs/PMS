using PMS.Domain.Entities;
using PMS.Application.Common.Models;

namespace PMS.Application.Common.Interfaces;

public interface ILabelRepository : IRepository<Label>
{
    Task<IReadOnlyList<Label>> ListAllOrderedAsync(CancellationToken ct = default);

    /// <summary>Có nhãn nào trùng tên chưa (bỏ qua <paramref name="excludingId"/> khi đang đổi tên).</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken ct = default);

    /// <summary>Nạp kèm <c>Tasks</c> — cần để EF gỡ hết bản ghi ở bảng nối khi xóa nhãn.</summary>
    Task<Label?> GetWithTasksAsync(Guid id, CancellationToken ct = default);
}
