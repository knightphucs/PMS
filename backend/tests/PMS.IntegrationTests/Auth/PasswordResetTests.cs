using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Auth;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Auth;

/// <summary>
/// Đặt lại mật khẩu (ADR-041). Phần lớn test ở đây bảo vệ tính <b>không phân biệt được</b>
/// của các phản hồi — thứ mà một bộ test chỉ kiểm "luồng thuận có chạy không" sẽ bỏ sót
/// hoàn toàn.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PasswordResetTests : IntegrationTestBase
{
    public PasswordResetTests(PmsWebApplicationFactory factory) : base(factory) { }

    private const string NewPassword = "Doi@Mk2026";

    [Fact]
    public async Task Doi_mat_khau_thanh_cong_thi_dang_nhap_duoc_bang_mat_khau_moi()
    {
        var user = await CreateUserAsync();

        var token = await RequestResetTokenAsync(user.Email);

        var reset = await ResetAsync(token, NewPassword);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Mật khẩu cũ chết
        (await LoginAsync(user.Email, "Test@1234")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // Mật khẩu mới sống
        (await LoginAsync(user.Email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Email_khong_ton_tai_van_tra_204_giong_het_email_co_that()
    {
        // 🔴 Đây là toàn bộ lý do endpoint này trả 204 thay vì 404: phân biệt được hai
        // trường hợp là biến nó thành công cụ dò xem ai đã đăng ký hệ thống.
        var existing = await CreateUserAsync();

        var forReal = await ForgotAsync(existing.Email);
        var forGhost = await ForgotAsync($"khong-ton-tai-{Guid.NewGuid():N}@pms.test");

        forReal.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        forGhost.StatusCode.ShouldBe(forReal.StatusCode);
    }

    [Fact]
    public async Task Token_dung_lan_hai_bi_tu_choi()
    {
        var user = await CreateUserAsync();
        var token = await RequestResetTokenAsync(user.Email);

        (await ResetAsync(token, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ResetAsync(token, "Khac@2026x")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_sai_va_token_da_dung_tra_ve_CUNG_MOT_loi()
    {
        // Nếu hai nguyên nhân cho ra hai thông điệp khác nhau thì kẻ tấn công biết được
        // token nào từng có thật — đủ để thu hẹp không gian đoán.
        var user = await CreateUserAsync();
        var token = await RequestResetTokenAsync(user.Email);
        await ResetAsync(token, NewPassword);

        var usedToken = await ResetAsync(token, "Khac@2026x");
        var bogusToken = await ResetAsync("token-bia-hoan-toan", "Khac@2026x");

        bogusToken.StatusCode.ShouldBe(usedToken.StatusCode);

        // So THÔNG ĐIỆP chứ không so nguyên thân phản hồi: ProblemDetails có `traceId` khác
        // nhau ở mỗi request, nên so chuỗi thô sẽ luôn đỏ vì một lý do không liên quan gì
        // tới thứ đang cần bảo vệ.
        (await ReadProblemTitleAsync(bogusToken))
            .ShouldBe(await ReadProblemTitleAsync(usedToken));
    }

    private static async Task<string?> ReadProblemTitleAsync(HttpResponseMessage response)
    {
        var problem = await response.Content
            .ReadFromJsonAsync<System.Text.Json.JsonElement>(TestJson.Options);
        return problem.TryGetProperty("title", out var title) ? title.GetString() : null;
    }

    [Fact]
    public async Task Cap_token_moi_thi_token_cu_het_hieu_luc()
    {
        // Mỗi lần yêu cầu chỉ có ĐÚNG MỘT link còn sống, nên bấm nhầm link cũ trong hộp
        // thư không đổi được mật khẩu.
        var user = await CreateUserAsync();

        var first = await RequestResetTokenAsync(user.Email);
        var second = await RequestResetTokenAsync(user.Email);

        first.ShouldNotBe(second);
        (await ResetAsync(first, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ResetAsync(second, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Doi_mat_khau_thu_hoi_toan_bo_phien_dang_dang_nhap()
    {
        // Tiền lệ ADR-015 (khóa tài khoản cũng thu hồi). Lý do đổi mật khẩu thường là
        // "nghi bị lộ", nên để phiên cũ sống tiếp là bỏ qua đúng mối đe dọa đang xử lý.
        var user = await CreateUserAsync();

        var beforeCount = await CountActiveRefreshTokensAsync(user.EmployeeId);
        beforeCount.ShouldBeGreaterThan(0);

        var token = await RequestResetTokenAsync(user.Email);
        (await ResetAsync(token, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await CountActiveRefreshTokensAsync(user.EmployeeId)).ShouldBe(0);
    }

    [Fact]
    public async Task Mat_khau_moi_yeu_bi_validator_tu_choi_400()
    {
        // Luồng "quên mật khẩu" không được là đường vòng để né chính sách mật khẩu.
        var user = await CreateUserAsync();
        var token = await RequestResetTokenAsync(user.Email);

        (await ResetAsync(token, "yeuqua")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chi_luu_HASH_cua_token_chu_khong_luu_token_tho()
    {
        var user = await CreateUserAsync();
        var token = await RequestResetTokenAsync(user.Email);

        // Kẻ đọc được database vẫn không đặt lại được mật khẩu của ai.
        var storedRaw = await WithDbAsync(db => db.PasswordResetTokens
            .AnyAsync(t => t.TokenHash == token));
        storedRaw.ShouldBeFalse();

        var hashLength = await WithDbAsync(db => db.PasswordResetTokens
            .Where(t => t.EmployeeId == user.EmployeeId)
            .Select(t => t.TokenHash.Length)
            .FirstAsync());
        hashLength.ShouldBe(64);   // SHA-256 dạng hex
    }

    // ---------- helpers ----------

    private Task<HttpResponseMessage> ForgotAsync(string email)
        => Factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new ForgotPasswordRequest(email));

    private Task<HttpResponseMessage> ResetAsync(string token, string newPassword)
        => Factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(token, newPassword, newPassword));

    private Task<HttpResponseMessage> LoginAsync(string email, string password)
        => Factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, password));

    /// <summary>
    /// Token thô chỉ tồn tại trong email (bản giả lập ghi ra log), nên test lấy nó bằng
    /// cách tự sinh lại: yêu cầu reset rồi đọc hash mới nhất từ DB và... không được — hash
    /// một chiều. Thay vào đó chèn thẳng một token đã biết qua DbContext, mô phỏng đúng
    /// những gì service vừa làm nhưng với giá trị thô nằm trong tầm tay của test.
    /// </summary>
    private async Task<string> RequestResetTokenAsync(string email)
    {
        (await ForgotAsync(email)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var rawToken = $"test-token-{Guid.NewGuid():N}";
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(rawToken)));

        await WithDbAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(e => e.Email == email);

            // Thay hash của token vừa cấp bằng hash của token test biết trước — giữ nguyên
            // mọi thứ khác (hạn, trạng thái chưa dùng) nên vẫn đi đúng luồng thật.
            var latest = await db.PasswordResetTokens
                .Where(t => t.EmployeeId == employee.Id && t.UsedAt == null)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();

            latest.TokenHash = hash;
            await db.SaveChangesAsync();
        });

        return rawToken;
    }

    private Task<int> CountActiveRefreshTokensAsync(Guid employeeId)
        => WithDbAsync(db => db.RefreshTokens
            .CountAsync(t => t.EmployeeId == employeeId
                          && t.RevokedAt == null
                          && t.ExpiresAt > DateTime.UtcNow));
}
