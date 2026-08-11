using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

public interface IActivityLogger
{
    /// <summary>Ghi nhật ký với tác nhân là người đang gọi request. Ném nếu chưa xác thực.</summary>
    void Log(string entityType, Guid entityId, ActivityAction action, string detail);

    /// <summary>
    /// Ghi nhật ký với tác nhân được chỉ định TƯỜNG MINH.
    ///
    /// <para>
    /// 🔴 Tồn tại vì nhóm sự kiện xác thực (ADR-058). Đăng nhập, đăng ký và đặt lại mật khẩu
    /// xảy ra trên request <b>ẩn danh</b>: <c>ICurrentUserService.EmployeeId</c> còn null tại
    /// thời điểm cần ghi, nên <see cref="Log"/> sẽ ném <c>UnauthorizedException</c> — chính
    /// giữa một luồng đang thành công. Ở những chỗ đó danh tính đã được xác lập bằng
    /// <i>nghiệp vụ</i> (vừa kiểm mật khẩu xong, vừa tạo xong tài khoản) chứ không bằng claim.
    /// </para>
    /// <para>
    /// ⚠️ Không dùng cho luồng đã đăng nhập chỉ để "tiện" — truyền tay một id ở đó là mở
    /// đường ghi nhật ký nhân danh người khác. Luồng có claim thì dùng <see cref="Log"/>.
    /// </para>
    /// </summary>
    void LogAs(Guid actorId, string entityType, Guid entityId, ActivityAction action, string detail);
}