namespace PMS.Domain.Entities;

/// <summary>
/// Một quyền cấp HỆ THỐNG (tầng 1) — ADR-045. Danh mục ĐÓNG: nguồn sự thật là
/// <c>SystemPermissions.All</c> bên Application, bảng này chỉ là bản sao được seed bằng
/// <c>HasData</c> để chính sách phân quyền đổi được bằng dữ liệu.
/// <para>
/// 🔴 <b>KHÔNG có quyền project-scoped nào ở đây.</b> Quyền tầng 2 (xem/sửa/xóa task,
/// quản lý thành viên…) vẫn đọc <c>ProjectMember.RoleInProject</c> tươi mỗi request qua
/// <c>ProjectAuthorizationService</c>. Nhét chúng vào đây là mở lại God Mode mà ADR-042
/// vừa đóng — có test khóa (<c>SystemPermissionsCatalogTests</c>).
/// </para>
/// <para>
/// <b>KHÔNG kế thừa <c>BaseEntity</c></b>, tiền lệ <see cref="ProjectTaskCounter"/> và
/// <see cref="Watcher"/>. Khóa chính là <see cref="Code"/> — khóa TỰ NHIÊN, cố ý: mã quyền
/// là cùng một chuỗi ở bốn nơi (cột này, giá trị claim <c>permission</c> trong JWT, trường
/// <c>permissions[]</c> của JSON, và hằng ở frontend). Dùng <c>Guid</c> làm khóa buộc phải
/// bịa GUID cứng chép vào hai khối <c>HasData</c>, và <c>Guid.NewGuid()</c> trong
/// <c>HasData</c> sinh lại mỗi lần scaffold nên đẻ ra migration rác mỗi lần build.
/// </para>
/// <para>
/// Không audit field, không soft-delete: đây là danh mục hạ tầng phân quyền chứ không phải
/// dữ liệu nghiệp vụ. Hệ quả: <c>ApplyIdNeverGenerated()</c> và <c>ApplyAuditFields()</c>
/// không đụng tới nó, và không query filter nào lọc nó đi.
/// </para>
/// </summary>
public class Permission
{
    /// <summary>
    /// Mã quyền dạng <c>resource:action</c>, chữ thường — ví dụ <c>employees:manage</c>.
    /// LÀ khóa chính.
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Mô tả tiếng Việt hiện cạnh mỗi ô tích ở màn <c>/admin/roles</c>. Bắt buộc: một ma
    /// trận checkbox toàn mã kỹ thuật không nói được cho người quản trị biết họ đang cấp gì.
    /// </summary>
    public string Description { get; set; } = null!;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
