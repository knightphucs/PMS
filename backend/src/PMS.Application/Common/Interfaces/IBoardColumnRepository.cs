using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IBoardColumnRepository : IRepository<BoardColumn>
{
    /// <summary>Mọi cột của project, đã sắp theo <c>Order</c> rồi tie-break bằng <c>Id</c>.</summary>
    Task<IReadOnlyList<BoardColumn>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Cột mặc định cho task MỚI — cột <c>Order</c> nhỏ nhất của project.
    /// <para>
    /// Cố ý suy từ thứ tự thay vì thêm cờ <c>IsDefault</c>: một cờ nữa là một bất biến nữa
    /// phải giữ (đúng một cột được bật, và nó không được nằm trong cột vừa bị xóa). Cột
    /// trái nhất là thứ người dùng vốn đã hiểu là điểm bắt đầu, và nó tự đúng khi họ kéo
    /// đổi thứ tự cột.
    /// </para>
    /// </summary>
    Task<BoardColumn?> GetDefaultForProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Số task đang đứng trong từng cột của project — nuôi <c>TaskCount</c> của DTO.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountTasksByColumnAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Chuyển MỌI task từ cột này sang cột kia bằng một lệnh UPDATE hàng loạt, đồng thời
    /// đồng bộ <c>Category</c>.
    /// <para>
    /// ⚠️ Không tải task lên bộ nhớ rồi lặp: một cột có thể chứa hàng nghìn task, và thao
    /// tác này chạy trong lúc người dùng đang chờ dialog xóa cột đóng lại.
    /// </para>
    /// </summary>
    Task<int> MoveAllTasksAsync(Guid fromColumnId, BoardColumn target, CancellationToken ct = default);

    /// <summary>
    /// Đồng bộ <c>TaskItem.Category</c> cho mọi task trong cột sau khi cột đổi nhóm.
    /// Đây là chốt giữ cho bản sao <c>Category</c> trên task không trôi khỏi cột.
    /// </summary>
    Task<int> SyncTaskCategoriesAsync(BoardColumn column, CancellationToken ct = default);
}
