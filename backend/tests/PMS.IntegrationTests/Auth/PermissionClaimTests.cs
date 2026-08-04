using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PMS.Application.Common.Authorization;
using PMS.Application.Features.Auth;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Auth;

/// <summary>
/// Quyền tầng 1 đi từ DB → claim JWT → thân phản hồi (ADR-045). Ba mặt phải luôn nói cùng
/// một câu.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PermissionClaimTests : IntegrationTestBase
{
    public PermissionClaimTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Dang_ky_tra_ve_quyen_cua_vai_tro_User()
    {
        var (client, auth) = await RegisterAsync();

        auth.Employee.Permissions.ShouldBe([SystemPermissions.ProjectsCreate]);
        client.Dispose();
    }

    /// <summary>
    /// 🔴 Bẫy đã được nhìn thấy TRƯỚC khi nó cắn: <c>AuthController.Me()</c> dựng
    /// <c>EmployeeDto</c> từ CLAIM chứ không đọc DB. Chỉ nối dây quyền ở <c>AuthService</c>
    /// thì <c>/auth/login</c> trả quyền thật còn <c>/auth/me</c> trả mảng rỗng — hai câu trả
    /// lời mâu thuẫn từ cùng một kiểu DTO, và người dùng thấy cái nào tùy vào việc họ vừa
    /// đăng nhập hay vừa F5. Không có gì bắt lỗi đó lúc biên dịch, nên nó phải có test.
    /// </summary>
    [Fact]
    public async Task auth_me_tra_ve_CUNG_danh_sach_quyen_voi_dang_nhap()
    {
        var (client, auth) = await RegisterAsync();

        var me = await client.GetFromJsonAsync<EmployeeDto>("/api/v1/auth/me", TestJson.Options);

        me!.Permissions.ShouldBe(auth.Employee.Permissions, ignoreOrder: true);
        me.Permissions.ShouldNotBeEmpty();
        client.Dispose();
    }

    [Fact]
    public async Task Token_mang_dung_cac_claim_permission()
    {
        var (client, auth) = await RegisterAsync();

        var claims = new JwtSecurityTokenHandler()
            .ReadJwtToken(auth.AccessToken)
            .Claims
            .Where(c => c.Type == SystemPermissions.ClaimType)
            .Select(c => c.Value)
            .ToList();

        // MỘT claim cho MỖI quyền, không phải một chuỗi gộp ngăn cách bằng dấu cách —
        // RequireClaim khớp trên claim lặp, đổi sang chuỗi gộp là làm mọi policy im lặng
        // ngừng khớp.
        claims.ShouldBe(auth.Employee.Permissions, ignoreOrder: true);
        client.Dispose();
    }

    [Fact]
    public async Task SystemAdmin_nhan_du_nam_quyen_trong_token()
    {
        var admin = await CreateSystemAdminAsync();

        var me = await admin.Client.GetFromJsonAsync<EmployeeDto>(
            "/api/v1/auth/me", TestJson.Options);

        me!.Permissions.ShouldBe(SystemPermissions.All, ignoreOrder: true);
    }

    /// <summary>
    /// Chứng minh <c>RefreshAsync</c> dựng lại tập quyền từ DB chứ không chép lại của token
    /// cũ — đây là thứ làm cho cửa sổ "tối đa 15 phút" thành sự thật thay vì một lời hứa.
    /// </summary>
    [Fact]
    public async Task Refresh_cap_lai_quyen_theo_DB_hien_tai()
    {
        var (client, auth) = await RegisterAsync();
        auth.Employee.Permissions.ShouldBe([SystemPermissions.ProjectsCreate]);

        // Gỡ projects:create khỏi vai trò User thẳng trong DB, rồi refresh.
        // ⚠️ try/finally: DB dùng chung cả collection — bỏ quên bước khôi phục là làm hỏng
        // mọi test class chạy sau (chúng đều tạo project bằng user thường).
        await SetUserRoleGrantAsync(granted: false);
        try
        {
            // Không có body: refresh token chỉ đến từ cookie httpOnly (ADR-027), và
            // CreateClient() của WebApplicationFactory giữ cookie giữa các request.
            var refreshed = await client.PostAsync("/api/v1/auth/refresh", null);
            refreshed.EnsureSuccessStatusCode();

            var next = await refreshed.Content
                .ReadFromJsonAsync<AuthenticatedResponse>(TestJson.Options);

            next!.Employee.Permissions.ShouldBeEmpty();
        }
        finally
        {
            await SetUserRoleGrantAsync(granted: true);
            client.Dispose();
        }
    }

    // ---------- helper ----------

    private async Task<(HttpClient Client, AuthenticatedResponse Auth)> RegisterAsync()
    {
        // ⚠️ BaseAddress https là BẮT BUỘC nếu test có gọi /auth/refresh: cookie refresh có
        // Secure=true nên CookieContainer sẽ KHÔNG gửi lại nó qua http, và BaseAddress mặc
        // định của WebApplicationFactory là http://localhost. Triệu chứng là 401 ở /refresh
        // trông y hệt "token sai" — bẫy này đã được ghi ở AuthCookieTests.
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var res = await client.PostAsJsonAsync("/api/v1/Auth/register", new RegisterRequest(
            "Perm User", $"perm-{Guid.NewGuid():N}@pms.test", "Test@1234", "Test@1234"));

        res.EnsureSuccessStatusCode();
        var auth = await res.Content.ReadFromJsonAsync<AuthenticatedResponse>(TestJson.Options);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return (client, auth);
    }

    private Task SetUserRoleGrantAsync(bool granted) => WithDbAsync(async db =>
    {
        var existing = await db.RolePermissions.FindAsync(
            SystemRole.User, SystemPermissions.ProjectsCreate);

        if (granted && existing is null)
            db.RolePermissions.Add(new RolePermission
            {
                SystemRole = SystemRole.User,
                PermissionCode = SystemPermissions.ProjectsCreate
            });
        else if (!granted && existing is not null)
            db.RolePermissions.Remove(existing);

        await db.SaveChangesAsync();
    });
}
