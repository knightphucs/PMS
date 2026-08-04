using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Authorization;
using PMS.Application.Features.Admin;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Admin;

/// <summary>
/// API quản trị ánh xạ vai trò → quyền (ADR-045).
///
/// <para>
/// 🔴 <b>Mọi test đổi dữ liệu phân quyền BẮT BUỘC khôi phục trong <c>finally</c>.</b> Cả
/// collection dùng CHUNG một database, nên để <c>SystemAdmin</c> mất <c>employees:manage</c>
/// là làm hỏng mọi test class chạy sau. Lưu ý thêm: chính lệnh PUT cũng thu hồi refresh token
/// của người gọi, nên bước khôi phục không được phụ thuộc vào phiên đó — dưới đây khôi phục
/// thẳng ở tầng DB.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class RolePermissionAdminTests : IntegrationTestBase
{
    public RolePermissionAdminTests(PmsWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// Dạng <c>[Theory]</c> trên danh sách route, cùng lý do với <c>SystemAdminScopeTests</c>:
    /// thêm một route admin mà quên bổ sung vào đây là khoảng trống <b>nhìn thấy được</b> khi
    /// review.
    /// </summary>
    public static TheoryData<string, string> AdminRoutes => new()
    {
        { "GET",    "/api/v1/admin/employees" },
        { "GET",    "/api/v1/admin/audit-logs" },
        { "GET",    "/api/v1/admin/permissions" },
        { "GET",    "/api/v1/admin/roles/permissions" },
        // Id không tồn tại là CỐ Ý: policy chạy TRƯỚC action, nên đúng đắn là 403 chứ không
        // phải 404. Khẳng định điều đó ở đây để nó là hành vi có chủ đích, không phải may mắn.
        { "PUT",    "/api/v1/labels/00000000-0000-0000-0000-000000000001" },
        { "DELETE", "/api/v1/labels/00000000-0000-0000-0000-000000000001" },
        { "PUT",    "/api/v1/admin/roles/User/permissions" }
    };

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task User_thuong_nhan_403_tren_moi_route_admin(string method, string url)
    {
        var user = await CreateUserAsync();

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "PUT")
            request.Content = JsonContent.Create(new { name = "x", color = "#112233", permissions = Array.Empty<string>() });

        var res = await user.Client.SendAsync(request);

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_doc_duoc_danh_muc_va_ma_tran()
    {
        var admin = await CreateSystemAdminAsync();

        var catalog = await admin.Client.GetFromJsonAsync<List<PermissionResponse>>(
            "/api/v1/admin/permissions", TestJson.Options);

        catalog.ShouldNotBeNull();
        catalog.Select(p => p.Code).ShouldBe(SystemPermissions.All, ignoreOrder: true);
        catalog.ShouldAllBe(p => p.Description.Length > 0);

        var matrix = await admin.Client.GetFromJsonAsync<List<RolePermissionsResponse>>(
            "/api/v1/admin/roles/permissions", TestJson.Options);

        // Mọi vai trò phải có mặt kể cả khi chưa được cấp quyền nào — thiếu dòng thì màn quản
        // trị không hiện vai trò đó và không còn cách nào cấp quyền cho nó.
        matrix.ShouldNotBeNull();
        matrix.Select(r => r.Role).ShouldBe(Enum.GetValues<SystemRole>(), ignoreOrder: true);
        matrix.Single(r => r.Role == SystemRole.User)
              .Permissions.ShouldBe([SystemPermissions.ProjectsCreate]);
    }

    /// <summary>
    /// Test hành vi cốt lõi: quyền nằm trong token nên đổi ở DB KHÔNG có hiệu lực tức thì.
    /// Khẳng định cả hai nửa — token cũ vẫn qua, token mới thì không — thay vì chỉ nửa dễ chịu.
    /// </summary>
    [Fact]
    public async Task Go_quyen_khoi_vai_tro_co_hieu_luc_o_TOKEN_KE_TIEP()
    {
        var actor = await CreateSystemAdminAsync();
        var victim = await CreateSystemAdminAsync();

        var reduced = SystemPermissions.All
            .Where(c => c != SystemPermissions.EmployeesManage)
            .ToList();

        try
        {
            var put = await actor.Client.PutAsJsonAsync(
                "/api/v1/admin/roles/SystemAdmin/permissions",
                new UpdateRolePermissionsRequest(reduced));

            put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // (1) Token ĐANG SỐNG vẫn mang quyền cũ — đây là cửa sổ ≤15 phút, nói thẳng chứ
            //     không giả vờ hệ thống nhất quán tức thì.
            (await victim.Client.GetAsync("/api/v1/admin/employees")).StatusCode
                .ShouldBe(HttpStatusCode.OK);

            // (2) Đăng nhập lại -> token mới -> quyền mới -> 403.
            var fresh = await LoginAsync(victim.Email);
            (await fresh.GetAsync("/api/v1/admin/employees")).StatusCode
                .ShouldBe(HttpStatusCode.Forbidden);
            fresh.Dispose();
        }
        finally
        {
            await RestoreSystemAdminGrantsAsync();
        }
    }

    [Fact]
    public async Task Doi_quyen_thu_hoi_refresh_token_cua_moi_nguoi_mang_vai_tro_do()
    {
        var actor = await CreateSystemAdminAsync();
        var bystander = await CreateSystemAdminAsync();

        try
        {
            await actor.Client.PutAsJsonAsync(
                "/api/v1/admin/roles/SystemAdmin/permissions",
                new UpdateRolePermissionsRequest(SystemPermissions.All.ToList()));

            var active = await WithDbAsync(db => db.RefreshTokens
                .AsNoTracking()
                .Where(rt => rt.EmployeeId == bystander.EmployeeId
                          && rt.RevokedAt == null
                          && rt.ExpiresAt > DateTime.UtcNow)
                .CountAsync());

            active.ShouldBe(0,
                "Đổi quyền của một vai trò phải thu hồi refresh token của MỌI người mang vai "
              + "trò đó — nếu không, cửa sổ dùng quyền cũ dài bằng tuổi refresh token (7 ngày) "
              + "chứ không phải tuổi access token (15 phút). Cùng lý do với ADR-015.");
        }
        finally
        {
            await RestoreSystemAdminGrantsAsync();
        }
    }

    [Fact]
    public async Task Khong_the_go_roles_manage_khoi_SystemAdmin()
    {
        var admin = await CreateSystemAdminAsync();

        var withoutSelfRecovery = SystemPermissions.All
            .Where(c => c != SystemPermissions.RolesManage)
            .ToList();

        var res = await admin.Client.PutAsJsonAsync(
            "/api/v1/admin/roles/SystemAdmin/permissions",
            new UpdateRolePermissionsRequest(withoutSelfRecovery));

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Không cần finally: 409 nghĩa là không có gì được ghi. Khẳng định luôn cho chắc.
        var actual = await GrantsForAsync(SystemRole.SystemAdmin);
        actual.ShouldContain(SystemPermissions.RolesManage);
    }

    [Fact]
    public async Task Ma_quyen_ngoai_danh_muc_bi_tu_choi_va_KHONG_ghi_gi()
    {
        var admin = await CreateSystemAdminAsync();
        var before = await GrantsForAsync(SystemRole.User);

        var res = await admin.Client.PutAsJsonAsync(
            "/api/v1/admin/roles/User/permissions",
            new UpdateRolePermissionsRequest([SystemPermissions.ProjectsCreate, "projects:read:all"]));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Tất-cả-hoặc-không-gì. Áp một phần rồi báo lỗi là trạng thái tệ nhất: người quản trị
        // tin là thao tác đã thất bại, trong khi phân quyền đã đổi.
        (await GrantsForAsync(SystemRole.User)).ShouldBe(before, ignoreOrder: true);
    }

    // ---------- helper ----------

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = Factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/Auth/login",
            new Application.Features.Auth.LoginRequest(email, "Test@1234"));

        res.EnsureSuccessStatusCode();
        var auth = await res.Content
            .ReadFromJsonAsync<Application.Features.Auth.AuthenticatedResponse>(TestJson.Options);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private Task<List<string>> GrantsForAsync(SystemRole role)
        => WithDbAsync(db => db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.SystemRole == role)
            .Select(rp => rp.PermissionCode)
            .ToListAsync());

    /// <summary>
    /// Trả <c>SystemAdmin</c> về đủ danh mục, thẳng ở tầng DB. KHÔNG đi qua API: lệnh PUT
    /// trước đó đã thu hồi phiên của mọi admin, nên gọi API ở đây là tự đặt bẫy.
    /// </summary>
    private Task RestoreSystemAdminGrantsAsync() => WithDbAsync(async db =>
    {
        var current = await db.RolePermissions
            .Where(rp => rp.SystemRole == SystemRole.SystemAdmin)
            .Select(rp => rp.PermissionCode)
            .ToListAsync();

        foreach (var code in SystemPermissions.All.Except(current))
            db.RolePermissions.Add(new Domain.Entities.RolePermission
            {
                SystemRole = SystemRole.SystemAdmin,
                PermissionCode = code
            });

        await db.SaveChangesAsync();
    });
}
