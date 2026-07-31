using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface ICommentRepository : IRepository<Comment>
{
    /// <summary>
    /// Comment của một task, nạp kèm <c>Author</c> để map ra <c>AuthorName</c>.
    /// Cũ nhất trước — hội thoại đọc theo thứ tự thời gian, khác hộp thông báo (mới nhất trước).
    /// </summary>
    Task<PagedResult<Comment>> GetPagedByTaskAsync(
        Guid taskId, PagedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Nạp kèm <c>Task</c> (cần <c>ProjectId</c> để kiểm quyền tầng 2) và <c>Author</c>
    /// (cần <c>Name</c> cho response). Có tracking để sửa/xóa được.
    /// </summary>
    Task<Comment?> GetWithTaskAsync(Guid id, CancellationToken ct = default);
}
