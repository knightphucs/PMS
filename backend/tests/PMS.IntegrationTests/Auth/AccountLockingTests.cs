using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Admin;
using PMS.Application.Features.Auth;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Auth;

public class AccountLockingTests : IntegrationTestBase
{
    public AccountLockingTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact] // KB28 — bằng chứng cho "khóa cửa sau", ca dễ sót nhất
    public async Task Tai_khoan_bi_khoa_khong_the_refresh_token_da_cap_truoc_do()
    {
        var admin = await CreateSystemAdminAsync();      // helper mới, xem bên dưới
        var victim = await CreateUserAsync();

        // Lấy refresh token HỢP LỆ trước khi bị khóa
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/Auth/login",
            new LoginRequest(victim.Email, "Test@1234"));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        await admin.Client.PostAsJsonAsync(
            $"/api/v1/admin/employees/{victim.EmployeeId}/lock",
            new LockAccountRequest("Vi phạm chính sách"));

        var refresh = await Factory.CreateClient().PostAsJsonAsync("/api/v1/Auth/refresh",
            new RefreshTokenRequest(auth!.RefreshToken));

        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Token phải bị thu hồi ngay lúc khóa, không đợi tới lúc dùng
        var stillActive = await WithDbAsync(db => db.RefreshTokens
            .AnyAsync(t => t.EmployeeId == victim.EmployeeId && t.RevokedAt == null));
        stillActive.ShouldBeFalse();
    }

    [Fact] // KB29
    public async Task Dang_nhap_khi_bi_khoa_tra_403_kem_ly_do_ro_rang()
    {
        var admin = await CreateSystemAdminAsync();
        var victim = await CreateUserAsync();

        await admin.Client.PostAsJsonAsync(
            $"/api/v1/admin/employees/{victim.EmployeeId}/lock",
            new LockAccountRequest("Vi phạm chính sách"));

        var res = await Factory.CreateClient().PostAsJsonAsync("/api/v1/Auth/login",
            new LoginRequest(victim.Email, "Test@1234"));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Sai mật khẩu vẫn phải là 401 chung chung -> không lộ trạng thái khóa
        // cho người không biết mật khẩu (chống user enumeration)
        var wrongPass = await Factory.CreateClient().PostAsJsonAsync("/api/v1/Auth/login",
            new LoginRequest(victim.Email, "SaiMatKhau@1"));
        wrongPass.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}