using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PMS.API.Controllers;
using PMS.Application.Features.Auth;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Api;

/// <summary>
/// Giữ ADR-027 (refresh token đi bằng cookie httpOnly thay vì thân JSON) khỏi bị vô hiệu
/// hóa im lặng.
///
/// ⚠️ Các test này đọc THẲNG header <c>Set-Cookie</c> chứ không qua <c>CookieContainer</c>.
/// Lý do giống hệt <c>EnumSerializationTests</c> phải đọc raw JSON: <c>CookieContainer</c>
/// nuốt mất thuộc tính cookie (nó thậm chí không enforce <c>SameSite</c>), nên test đi qua
/// nó sẽ vẫn xanh dù ai đó tháo mất <c>HttpOnly</c> hay <c>Secure</c> — tức là không bảo vệ
/// được đúng cái cần bảo vệ.
/// </summary>
public class AuthCookieTests : IntegrationTestBase
{
    public AuthCookieTests(PmsWebApplicationFactory factory) : base(factory) { }

    private static HttpClient SecureClient(PmsWebApplicationFactory factory)
        // Cookie có Secure=true nên CookieContainer chỉ gửi lại qua https; BaseAddress
        // mặc định của WebApplicationFactory là http://localhost.
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static RegisterRequest NewUser() =>
        new($"Cookie User", $"cookie-{Guid.NewGuid():N}@pms.test", "Test@1234", "Test@1234");

    [Fact]
    public async Task Dang_ky_dat_cookie_refresh_du_bon_thuoc_tinh_bao_ve()
    {
        var res = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", NewUser());
        res.EnsureSuccessStatusCode();

        var setCookie = res.Headers.GetValues("Set-Cookie")
            .SingleOrDefault(c => c.StartsWith($"{AuthController.RefreshCookieName}="));

        setCookie.ShouldNotBeNull("Không có Set-Cookie cho refresh token — ADR-027 bị vô hiệu hóa.");
        setCookie.ShouldContain("httponly", Case.Insensitive);       // XSS không đọc được
        setCookie.ShouldContain("secure", Case.Insensitive);         // chỉ đi trên HTTPS
        setCookie.ShouldContain("samesite=strict", Case.Insensitive); // chặn CSRF tới /refresh
        setCookie.ShouldContain($"path={AuthController.RefreshCookiePath}", Case.Insensitive);
    }

    /// <summary>
    /// Chốt chặn thật của ADR-027. Nếu refresh token quay lại thân phản hồi thì XSS chỉ
    /// cần gọi /auth/refresh với credentials:'include' rồi đọc body là chiếm được phiên
    /// 7 ngày — cookie httpOnly mất sạch tác dụng mà không test nào khác đỏ.
    /// </summary>
    [Fact]
    public async Task Than_phan_hoi_khong_duoc_chua_refresh_token()
    {
        var res = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", NewUser());
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadAsStringAsync();

        body.ShouldNotContain("refreshToken", Case.Insensitive);
        body.ShouldContain("accessToken", Case.Insensitive);   // vẫn phải trả access token
    }

    [Fact]
    public async Task Refresh_khong_kem_cookie_tra_401()
    {
        var res = await Factory.CreateClient().PostAsync("/api/v1/auth/refresh", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_bang_cookie_cap_access_token_moi_va_xoay_cookie()
    {
        var client = SecureClient(Factory);

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", NewUser());
        register.EnsureSuccessStatusCode();
        var first = await register.Content.ReadFromJsonAsync<AuthenticatedResponse>(TestJson.Options);

        var refresh = await client.PostAsync("/api/v1/auth/refresh", null);

        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await refresh.Content.ReadFromJsonAsync<AuthenticatedResponse>(TestJson.Options);
        second!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        second.Employee.Email.ShouldBe(first!.Employee.Email);

        // Rotation: mỗi lần refresh phải phát lại cookie với token MỚI, nếu không thì
        // lần refresh kế tiếp dùng lại token cũ và kích hoạt reuse detection.
        refresh.Headers.GetValues("Set-Cookie")
            .ShouldContain(c => c.StartsWith($"{AuthController.RefreshCookieName}="));
    }

    /// <summary>
    /// Đăng xuất phải xóa được cookie. Bốn thuộc tính lúc xóa phải khớp nguyên vẹn lúc đặt,
    /// nếu không trình duyệt coi là hai cookie khác nhau và cookie cũ vẫn sống tiếp.
    /// </summary>
    [Fact]
    public async Task Dang_xuat_xoa_cookie_va_refresh_sau_do_that_bai()
    {
        var client = SecureClient(Factory);

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", NewUser());
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<AuthenticatedResponse>(TestJson.Options);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refresh = await client.PostAsync("/api/v1/auth/refresh", null);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
