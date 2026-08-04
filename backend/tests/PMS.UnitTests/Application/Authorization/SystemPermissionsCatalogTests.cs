using System.Reflection;
using System.Text.RegularExpressions;
using PMS.Application.Common.Authorization;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Authorization;

/// <summary>
/// Khóa danh mục quyền tầng 1 (ADR-045). Đây là chốt chặn chống mở lại "god mode" mà ADR-042
/// vừa đóng — mô hình permission có một cám dỗ rất tự nhiên là thêm <c>projects:read:all</c>,
/// và những test dưới đây làm cho việc đó không lọt qua review trong im lặng.
/// </summary>
public class SystemPermissionsCatalogTests
{
    /// <summary>
    /// Danh mục ĐÓNG, viết tường minh trong test. Thêm / xóa / đổi tên bất kỳ mã nào cũng làm
    /// test này đỏ, buộc phải sửa cả ở đây — tức buộc một con người gật đầu với thay đổi phân
    /// quyền, thay vì nó trôi theo một commit về việc khác.
    /// </summary>
    private static readonly string[] ExpectedCatalog =
    [
        "employees:manage",
        "audit:read",
        "labels:manage",
        "projects:create",
        "roles:manage"
    ];

    [Fact]
    public void Danh_muc_quyen_la_DONG()
    {
        SystemPermissions.All.ShouldBe(ExpectedCatalog, ignoreOrder: true);
    }

    [Fact]
    public void Moi_hang_const_deu_phai_nam_trong_All()
    {
        // Bắt đúng lỗi "khai const, gắn vào [Authorize], nhưng quên thêm vào All" — hậu quả
        // là policy không bao giờ được đăng ký và endpoint đó ném 500 lúc chạy.
        var declared = typeof(SystemPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Where(f => f.Name != nameof(SystemPermissions.ClaimType))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        declared.ShouldBe(SystemPermissions.All, ignoreOrder: true);
    }

    /// <summary>
    /// 🔴 Khóa god mode. Quyền tầng 1 KHÔNG được mang phạm vi project — quyền project-scoped
    /// phải đi qua <c>ProjectAuthorizationService</c> đọc <c>ProjectMember.RoleInProject</c>
    /// tươi mỗi request, vì một người có vai trò khác nhau ở từng project và PM đổi vai trò
    /// phải có hiệu lực tức thì.
    /// </summary>
    [Fact]
    public void Khong_ma_nao_mang_pham_vi_project()
    {
        // Tài nguyên thuộc miền project. Một mã tầng 1 chạm vào bất kỳ cái nào trong đây
        // nghĩa là nó đang trả lời một câu hỏi mà chỉ ProjectMember mới trả lời được.
        string[] projectScopedResources =
        [
            "project", "projects", "task", "tasks", "subtask", "subtasks",
            "sprint", "sprints", "member", "members", "comment", "comments",
            "attachment", "attachments", "watcher", "watchers", "link", "links",
            "board", "backlog", "statistics", "activity"
        ];

        foreach (var code in SystemPermissions.All)
        {
            var resource = code.Split(':')[0];

            if (!projectScopedResources.Contains(resource)) continue;

            // NGOẠI LỆ DUY NHẤT, gọi đích danh: lúc tạo project thì CHƯA CÓ project nào để
            // tra membership, nên không có tầng 2 nào để đi qua. Mọi động từ project khác đều
            // cần một project đã tồn tại. Viết đích danh chứ không nới regex, để
            // `projects:read` hay `projects:read:all` vẫn đỏ ngay.
            code.ShouldBe(
                SystemPermissions.ProjectsCreate,
                $"`{code}` mang phạm vi project. Quyền project-scoped phải nằm ở "
              + "ProjectPermissions (tầng 2, đọc ProjectMember mỗi request), không phải trong "
              + "JWT claim — xem ADR-042 và ADR-045. Ngoại lệ duy nhất là projects:create.");
        }
    }

    [Fact]
    public void Ma_quyen_dung_dinh_dang_resource_action()
    {
        foreach (var code in SystemPermissions.All)
            Regex.IsMatch(code, "^[a-z]+(-[a-z]+)*:[a-z]+(-[a-z]+)*$")
                 .ShouldBeTrue($"`{code}` không đúng dạng `resource:action` toàn chữ thường.");
    }

    [Fact]
    public void Khong_co_ma_trung_lap()
    {
        SystemPermissions.All.Distinct().Count().ShouldBe(SystemPermissions.All.Count);
    }

    [Fact]
    public void Claim_type_la_permission()
    {
        // Chuỗi này phải khớp chính xác với hằng ở frontend (lib/auth/system-permissions.ts)
        // và với vòng lặp đăng ký policy trong Program.cs.
        SystemPermissions.ClaimType.ShouldBe("permission");
    }
}
