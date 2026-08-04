using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IEmployeeRepository : IRepository<Employee>
{    
    Task<Employee?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<PagedResult<Employee>> GetPagedAsync(PagedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Tra nhân viên theo tên hoặc email, cho MỌI người dùng đã đăng nhập (không cần quyền
    /// admin) — phục vụ ô gợi ý khi mời thành viên vào dự án.
    /// <para>
    /// 🔴 <paramref name="keyword"/> là <b>bắt buộc và tối thiểu 2 ký tự</b>, và
    /// <paramref name="limit"/> có trần cứng. Không có hai ràng buộc đó thì đây là API dump
    /// toàn bộ danh bạ công ty cho bất kỳ tài khoản nào — khác hẳn
    /// <see cref="GetPagedAsync"/> vốn nằm sau quyền <c>employees:manage</c>.
    /// </para>
    /// <para>
    /// Chỉ trả người <b>chưa bị khóa</b>: mời một tài khoản đã khóa vào dự án tạo ra một
    /// thành viên không bao giờ đăng nhập được.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Employee>> SearchActiveAsync(
        string keyword, int limit, CancellationToken ct = default);

    /// <summary>Số SystemAdmin chưa bị khóa, không tính người đang bị thao tác.</summary>
    Task<int> CountActiveAdminsExceptAsync(Guid excludingId, CancellationToken ct = default);
}