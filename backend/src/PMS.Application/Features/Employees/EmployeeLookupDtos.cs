namespace PMS.Application.Features.Employees;

/// <summary>
/// Kết quả tra nhân viên cho ô gợi ý khi mời thành viên.
///
/// <para>
/// 🔴 Cố ý CHỈ có ba trường. So với <c>EmployeeAdminResponse</c>, DTO này thiếu
/// <c>systemRole</c>, <c>isLocked</c>, <c>lockedAt</c>, <c>lockReason</c>, <c>createdAt</c> —
/// và đó là toàn bộ điểm khác biệt, không phải sự lười biếng. Endpoint này mở cho **mọi**
/// người dùng đã đăng nhập, nên mỗi trường thêm vào là một mẩu thông tin nhân sự phát cho
/// toàn công ty. Ai cần những trường kia thì đi qua <c>employees:manage</c>.
/// </para>
/// <para>
/// <c>email</c> vẫn có mặt vì nó là thứ phân biệt hai người trùng tên — thiếu nó thì ô gợi ý
/// không dùng được. Đây là đánh đổi có ý thức, không phải bỏ sót.
/// </para>
/// </summary>
public record EmployeeLookupResponse(Guid Id, string Name, string Email);
