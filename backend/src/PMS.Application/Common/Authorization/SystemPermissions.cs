namespace PMS.Application.Common.Authorization;

/// <summary>
/// Danh mục ĐÓNG các quyền cấp HỆ THỐNG (tầng 1) — ADR-045.
///
/// <para>
/// 🔴 <b>Luật bất di bất dịch: không mã nào ở đây được mang phạm vi project.</b> Quyền tầng 2
/// (xem/sửa task, quản lý thành viên, xem board…) đọc <c>ProjectMember.RoleInProject</c>
/// TƯƠI mỗi request qua <c>ProjectAuthorizationService</c> + <see cref="ProjectPermissions"/>,
/// và phải ở nguyên đó vì hai lý do: một người có vai trò khác nhau ở từng project (nhét vào
/// token là token phình theo số project), và PM đổi vai trò của ai đó phải có hiệu lực TỨC
/// THÌ chứ không phải sau tối đa 15 phút.
/// </para>
/// <para>
/// Cám dỗ tự nhiên của mô hình permission là thêm <c>projects:read:all</c> cho admin. Đó
/// chính là God Mode mà ADR-042 vừa đóng lại (SystemAdmin không có <b>bất kỳ</b> đặc quyền
/// nghiệp vụ nào; không phải thành viên thì nhận 404 y hệt người ngoài). Thêm nó là làm cả
/// <c>SystemAdminScopeTests</c> lẫn <c>SystemPermissionsCatalogTests</c> đỏ — cố ý.
/// </para>
/// <para>
/// <b>Vì sao <c>const string</c> chứ không phải enum:</b> <c>[Authorize(Policy = ...)]</c> cần
/// hằng biên dịch. Quan trọng hơn — mã quyền là CÙNG MỘT CHUỖI ở bốn nơi: cột
/// <c>Permissions.Code</c>, giá trị claim <c>permission</c> trong JWT, trường
/// <c>permissions[]</c> của JSON, và hằng ở <c>frontend/lib/auth/system-permissions.ts</c>.
/// Enum sẽ chèn thêm một tầng phiên dịch, tức thêm hai chỗ nữa để lệch.
/// <see cref="ProjectPermissions"/> (tầng 2) thì dùng enum + switch — khác hình dạng là cố
/// ý, nó khiến người đọc nhìn một cái là biết đang ở tầng nào.
/// </para>
/// <para>
/// <b>Thêm quyền mới cần ĐỦ BA bước:</b> thêm <c>const</c> ở đây → thêm vào <see cref="All"/>
/// → thêm vào <c>HasData</c> của <c>PermissionConfiguration</c> rồi
/// <c>dotnet ef migrations add</c>. Quên bước 3 thì <c>has-pending-model-changes</c> đỏ; quên
/// bước 2 thì <c>SystemPermissionsCatalogTests</c> đỏ. Không có đường nào lọt im lặng.
/// </para>
/// </summary>
public static class SystemPermissions
{
    /// <summary>
    /// Kiểu claim mang quyền trong JWT. <c>Program.cs</c> đặt
    /// <c>MapInboundClaims = false</c> nên chuỗi này sống nguyên vẹn tới
    /// <c>ClaimsPrincipal</c>, không bị ánh xạ sang URI dài.
    /// </summary>
    public const string ClaimType = "permission";

    /// <summary>
    /// Quản lý nhân sự: xem danh sách, khóa/mở tài khoản, đổi <c>SystemRole</c> của người khác.
    /// <para>
    /// Cố ý là MỘT mã chứ không tách <c>employees:read</c> / <c>employees:lock</c> /
    /// <c>employees:change-role</c>: <c>[Authorize]</c> hôm nay nằm ở class-level của
    /// <c>AdminEmployeesController</c> và gác cả bốn action như nhau, nên tách ra là ĐỔI HÀNH
    /// VI giấu bên trong một refactor. Tách khi có màn hình thật cần vai trò "kiểm toán viên
    /// chỉ đọc".
    /// </para>
    /// </summary>
    public const string EmployeesManage = "employees:manage";

    /// <summary>Đọc nhật ký cấp hệ thống (<c>GET /admin/audit-logs</c>).</summary>
    public const string AuditRead = "audit:read";

    /// <summary>
    /// Sửa/xóa nhãn TOÀN CỤC. Gác <c>PUT</c>/<c>DELETE /labels/{id}</c>, cố ý KHÔNG gác
    /// <c>POST /labels</c> — bất đối xứng đã có từ trước và có lý do ghi ở
    /// <c>LabelsController</c>: tạo nhãn là thao tác cộng thêm, còn xóa một nhãn gỡ chip khỏi
    /// board của MỌI project.
    /// </summary>
    public const string LabelsManage = "labels:manage";

    /// <summary>
    /// Tạo project mới. Cấp cho <b>cả</b> <c>User</c> lẫn <c>SystemAdmin</c> — thay thế policy
    /// <c>can-create-project</c> cũ vốn chỉ là <c>RequireAuthenticatedUser()</c>, tức một
    /// no-op. Giữ nguyên hành vi: mọi người dùng đều tạo được project và tự thành PM của nó.
    /// <para>
    /// 🔴 Đây là mã <c>projects:*</c> DUY NHẤT được phép tồn tại, và nó KHÔNG vi phạm luật
    /// "không có quyền project-scoped": lúc gọi endpoint này <b>chưa có project nào</b> để tra
    /// membership, nên không có tầng 2 để đi qua. Mọi động từ project khác đều cần một project
    /// đã tồn tại → đi qua <c>ProjectAuthorizationService</c>.
    /// <c>SystemPermissionsCatalogTests</c> gọi đích danh ngoại lệ này, nên
    /// <c>projects:read</c> hay <c>projects:read:all</c> vẫn đỏ ngay.
    /// </para>
    /// </summary>
    public const string ProjectsCreate = "projects:create";

    /// <summary>
    /// Sửa ánh xạ vai trò → quyền (<c>/admin/roles</c>).
    /// <para>
    /// Đây là quyền TỰ PHỤC HỒI duy nhất: có nó thì cấp lại được mọi quyền khác qua UI. Vì
    /// vậy <c>RolePermissionAdminService</c> có bất biến "SystemAdmin luôn giữ
    /// <c>roles:manage</c>" (409) — gỡ nó là khóa vĩnh viễn mọi lối vào quản trị, vì
    /// <c>DbSeeder</c> không chạy ở production và <c>HasData</c> chỉ áp lúc migrate mới.
    /// </para>
    /// </summary>
    public const string RolesManage = "roles:manage";

    /// <summary>
    /// Toàn bộ danh mục. Dùng để đăng ký policy (<c>Program.cs</c>), để validate đầu vào của
    /// API phân quyền, và làm mốc so cho test khóa danh mục.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        EmployeesManage,
        AuditRead,
        LabelsManage,
        ProjectsCreate,
        RolesManage
    ];
}
