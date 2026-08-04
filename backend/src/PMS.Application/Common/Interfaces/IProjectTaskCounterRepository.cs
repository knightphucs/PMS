using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Cấp số thứ tự task cho một project (ADR-033). <b>Không</b> kế thừa
/// <see cref="IRepository{T}"/> vì <see cref="ProjectTaskCounter"/> không phải
/// <c>BaseEntity</c> — tiền lệ <c>IWatcherRepository</c>.
/// </summary>
public interface IProjectTaskCounterRepository
{
    /// <summary>
    /// Tăng bộ đếm của project lên 1 và trả về giá trị MỚI, nguyên tử.
    /// <para>
    /// 🔴 Phải gọi bên trong <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>: câu
    /// <c>UPDATE … OUTPUT</c> giữ X lock trên hàng bộ đếm cho tới hết transaction, nên hai
    /// người tạo task cùng lúc thì người thứ hai <b>chờ một nhịp</b> chứ không nhận lỗi.
    /// Gọi ngoài transaction thì lock nhả ngay và bảo đảm biến mất.
    /// </para>
    /// </summary>
    Task<int> NextNumberAsync(Guid projectId, CancellationToken ct = default);

    void Add(ProjectTaskCounter counter);
}
