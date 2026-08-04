using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Ánh xạ <see cref="SystemRole"/> → <see cref="Permission"/> (ADR-045). Đây là thứ làm cho
/// "admin sửa được quyền của một vai trò" thành thao tác trên DỮ LIỆU thay vì sửa một
/// <c>switch</c> trong C# rồi phải deploy lại.
/// <para>
/// <b>Không</b> kế thừa <c>BaseEntity</c>. Khóa chính ghép <c>(SystemRole, PermissionCode)</c>
/// — chính nó LÀ ràng buộc duy nhất, nên cấp trùng một quyền là bất khả thi về mặt vật lý
/// chứ không phải một luật validate ai đó có thể quên viết.
/// </para>
/// <para>
/// Cố ý KHÔNG có bảng cấp quyền cho từng người (<c>EmployeePermission</c>): quyền tầng 1
/// gắn với vai trò, và thêm một trục cấp phát thứ hai là thêm một bất biến "còn ≥1 người
/// giữ quyền X" phải bảo vệ ở mọi đường ghi. Khi nào có nhu cầu thật thì thêm, cùng với
/// người dùng đầu tiên của nó.
/// </para>
/// </summary>
public class RolePermission
{
    public SystemRole SystemRole { get; set; }

    public string PermissionCode { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}
