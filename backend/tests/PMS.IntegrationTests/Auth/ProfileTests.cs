using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Auth;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Auth;

/// <summary>
/// Đường ghi hồ sơ cá nhân (ADR-049). Trọng tâm không phải "luồng thuận có chạy không" mà
/// là chứng minh đường phát-lại-token chạy ĐẦU-CUỐI: đổi tên xong, gọi lại <c>/auth/me</c>
/// bằng access token MỚI phải thấy tên mới ngay — nếu thiếu bước phát-lại-token, test này
/// sẽ đỏ đúng chỗ mà một bản cài đặt ngây thơ (chỉ lưu DB, không đổi token) sẽ sai.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ProfileTests : IntegrationTestBase
{
    public ProfileTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Doi_ten_thanh_cong_va_token_moi_phan_anh_ngay_khong_can_refresh()
    {
        var user = await CreateUserAsync();

        var res = await user.Client.PutAsJsonAsync(
            "/api/v1/auth/profile", new UpdateProfileRequest("Tên Mới"));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await res.Content.ReadFromJsonAsync<AuthenticatedResponse>(TestJson.Options);
        auth!.Employee.Name.ShouldBe("Tên Mới");

        // Bằng chứng đầu-cuối: gọi /auth/me bằng CHÍNH access token vừa nhận, không refresh.
        user.Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var me = await user.Client.GetFromJsonAsync<EmployeeDto>("/api/v1/auth/me", TestJson.Options);
        me!.Name.ShouldBe("Tên Mới");

        // Và DB thật sự đã ghi, không chỉ token nói vậy.
        var dbName = await WithDbAsync(db => db.Employees
            .Where(e => e.Id == user.EmployeeId).Select(e => e.Name).SingleAsync());
        dbName.ShouldBe("Tên Mới");
    }

    [Fact]
    public async Task Ten_rong_bi_tu_choi_400()
    {
        var user = await CreateUserAsync();

        var res = await user.Client.PutAsJsonAsync(
            "/api/v1/auth/profile", new UpdateProfileRequest("   "));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chua_dang_nhap_thi_401()
    {
        var res = await Factory.CreateClient().PutAsJsonAsync(
            "/api/v1/auth/profile", new UpdateProfileRequest("Ai đó"));

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Doi_mat_khau_thanh_cong_thi_dang_nhap_duoc_bang_mat_khau_moi_va_van_con_phien_hien_tai()
    {
        var user = await CreateUserAsync();

        var res = await user.Client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest("Test@1234", "Moi@Mk2026", "Moi@Mk2026"));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await res.Content.ReadFromJsonAsync<AuthenticatedResponse>(TestJson.Options);
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();

        // Mật khẩu cũ chết, mật khẩu mới sống.
        var oldLogin = await Factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(user.Email, "Test@1234"));
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newLogin = await Factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(user.Email, "Moi@Mk2026"));
        newLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Doi_mat_khau_sai_mat_khau_hien_tai_bi_tu_choi_400()
    {
        var user = await CreateUserAsync();

        var res = await user.Client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest("Sai@Mk1234", "Moi@Mk2026", "Moi@Mk2026"));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Doi_mat_khau_thu_hoi_phien_KHAC_nhung_khong_thu_hoi_chinh_phien_dang_thuc_hien()
    {
        var user = await CreateUserAsync();

        // Một "phiên khác" — đăng nhập lần hai để có refresh token thứ hai đang active.
        var secondLogin = await Factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(user.Email, "Test@1234"));
        secondLogin.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await CountActiveRefreshTokensAsync(user.EmployeeId)).ShouldBeGreaterThanOrEqualTo(2);

        var res = await user.Client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest("Test@1234", "Moi@Mk2026", "Moi@Mk2026"));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Đúng MỘT phiên active còn lại — phiên vừa được phát lại cho request này.
        (await CountActiveRefreshTokensAsync(user.EmployeeId)).ShouldBe(1);
    }

    private Task<int> CountActiveRefreshTokensAsync(Guid employeeId)
        => WithDbAsync(db => db.RefreshTokens
            .CountAsync(t => t.EmployeeId == employeeId
                          && t.RevokedAt == null
                          && t.ExpiresAt > DateTime.UtcNow));
}
