using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Auth;

[Mapper]
public partial class EmployeeMapper
{
    // `Permissions` cố ý bỏ trống: nó không phải thuộc tính của Employee mà là tập quyền suy
    // từ SystemRole qua bảng RolePermissions (ADR-045). Người gọi điền bằng
    // `ToDto(e) with { Permissions = ... }`.
    //
    // Dùng [MapperIgnoreTarget] chứ KHÔNG nới #pragma bên dưới: đây là RMG012 (target chưa
    // map), còn pragma đó tắt RMG020 (source chưa map) — che một chẩn đoán bằng pragma của
    // chẩn đoán khác là giấu luôn cái tiếp theo.
    // ⚠️ `#pragma disable` phải nằm TRƯỚC attribute: span của chẩn đoán bắt đầu ở danh sách
    // attribute chứ không ở dòng khai báo method, nên đặt pragma xen giữa hai thứ đó là để
    // cảnh báo rơi ra ngoài vùng tắt (đã thấy thật: 15 cảnh báo RMG020 quay lại).
#pragma warning disable RMG020 // Source member is not mapped to any target member
    [MapperIgnoreTarget(nameof(EmployeeDto.Permissions))]
    public partial EmployeeDto ToDto(Employee employee);
#pragma warning restore RMG020 // Source member is not mapped to any target member
}