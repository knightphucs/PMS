using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.ActivityLogs;
using PMS.Application.Features.Auth;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Auth;

/// <summary>
/// Nhật ký kiểm toán cho nhóm sự kiện xác thực (ADR-058).
///
/// <para>
/// 🔴 <b>Vì sao nhóm này phải có mặt trong <c>ActivityLog</c> chứ không chỉ Serilog:</b>
/// <c>/admin/audit-logs</c> tồn tại để SystemAdmin giải trình được việc mình làm (ADR-042).
/// Nhưng cho tới 2026-08-11, đăng nhập / đăng xuất / đổi & đặt lại mật khẩu — đúng những
/// dòng mà kiểm toán nội bộ ngân hàng hỏi tới đầu tiên — hoàn toàn KHÔNG có ở đó; chúng chỉ
/// nằm trong file log dạng văn bản, thứ không truy vấn được và xoay vòng theo ngày.
/// </para>
/// <para>
/// Bộ test này chạy qua HTTP thật vì phần khó nằm ở chỗ <c>ActivityLogger.Log</c> đọc
/// <c>ICurrentUserService</c> — mà ba trong sáu luồng dưới đây là request ẨN DANH.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AuthAuditTrailTests : IntegrationTestBase
{
    public AuthAuditTrailTests(PmsWebApplicationFactory factory) : base(factory) { }

    private Task<List<(ActivityAction Action, string Detail, Guid ActorId)>> AuditOfAsync(Guid employeeId)
        => WithDbAsync(db => db.ActivityLogs
            .AsNoTracking()
            .Where(l => l.EntityType == "Employee" && l.EntityId == employeeId)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new ValueTuple<ActivityAction, string, Guid>(l.Action, l.Detail!, l.EmployeeId))
            .ToListAsync());

    [Fact]
    public async Task Dang_ky_va_dang_nhap_deu_sinh_dong_kiem_toan_du_request_la_AN_DANH()
    {
        var user = await CreateUserAsync();   // đi qua POST /auth/register

        // Đăng nhập lần nữa để có cả hai loại sự kiện trên cùng một tài khoản.
        (await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(user.Email, "Test@1234")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var audit = await AuditOfAsync(user.EmployeeId);

        audit.Select(a => a.Action).ShouldBe(
            [ActivityAction.Registered, ActivityAction.LoggedIn]);

        // Tác nhân là CHÍNH người đó, không phải null hay một id bịa: đây là điều
        // ActivityLogger.Log() không làm được vì lúc đăng ký chưa có claim nào.
        audit.ShouldAllBe(a => a.ActorId == user.EmployeeId);
    }

    [Fact]
    public async Task Dang_nhap_sai_mat_khau_van_ghi_lai_du_request_ket_thuc_bang_exception()
    {
        var user = await CreateUserAsync();

        var res = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(user.Email, "SaiHoanToan@9999"));
        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // 🔴 Phép kiểm đắt giá nhất file này. Nhánh sai mật khẩu kết thúc bằng
        // UnauthorizedException, nên nếu quên SaveChanges trước khi ném thì dòng này biến
        // mất cùng request — và đó đúng là dòng mà kiểm toán cần nhất.
        var audit = await AuditOfAsync(user.EmployeeId);

        audit.Select(a => a.Action).ShouldContain(ActivityAction.LoginFailed);
        audit.Single(a => a.Action == ActivityAction.LoginFailed)
             .Detail.ShouldContain("sai mật khẩu");
    }

    [Fact]
    public async Task Dang_nhap_bang_email_khong_ton_tai_KHONG_ghi_dong_nao()
    {
        var before = await WithDbAsync(db => db.ActivityLogs.CountAsync());

        (await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest($"khong-ton-tai-{Guid.NewGuid():N}@pms.test", "Test@1234")))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Không có Employee thì không có EmployeeId để quy trách nhiệm, mà cột đó là khóa
        // ngoại bắt buộc. Bịa một "employee vô danh" để lấp chỗ trống sẽ làm hỏng chính bảng
        // đang dùng để quy trách nhiệm — nên trường hợp này CỐ Ý chỉ nằm ở Serilog (ADR-058).
        (await WithDbAsync(db => db.ActivityLogs.CountAsync())).ShouldBe(before);
    }

    [Fact]
    public async Task Doi_mat_khau_va_doi_ten_ho_so_deu_duoc_ghi_lai()
    {
        var user = await CreateUserAsync();

        (await user.Client.PutAsJsonAsync("/api/v1/auth/profile",
            new UpdateProfileRequest("Tên Mới")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await user.Client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest("Test@1234", "MatKhauMoi@2026", "MatKhauMoi@2026")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var audit = await AuditOfAsync(user.EmployeeId);

        audit.Select(a => a.Action).ShouldContain(ActivityAction.ProfileUpdated);
        audit.Select(a => a.Action).ShouldContain(ActivityAction.PasswordChanged);

        // Đổi tên phải ghi lại GIÁ TRỊ CŨ — "đã đổi tên" mà không nói đổi từ gì thì
        // không dựng lại được dòng thời gian, tức là vô dụng với kiểm toán.
        audit.Single(a => a.Action == ActivityAction.ProfileUpdated)
             .Detail.ShouldContain("Test User");
    }

    [Fact]
    public async Task Quen_va_dat_lai_mat_khau_ghi_du_ca_hai_dau_cua_luong()
    {
        var user = await CreateUserAsync();
        var anon = Factory.CreateClient();

        (await anon.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new ForgotPasswordRequest(user.Email)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Token thô không bao giờ rời server, nên lấy từ DB — đúng cách các test ADR-041 khác làm.
        var rawToken = Guid.NewGuid().ToString("N");
        await WithDbAsync(async db =>
        {
            var token = await db.PasswordResetTokens
                .Where(t => t.EmployeeId == user.EmployeeId)
                .OrderByDescending(t => t.CreatedAt).FirstAsync();
            token.TokenHash = Sha256(rawToken);
            await db.SaveChangesAsync();
        });

        (await anon.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordRequest(rawToken, "MatKhauMoi@2026", "MatKhauMoi@2026")))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var actions = (await AuditOfAsync(user.EmployeeId)).Select(a => a.Action).ToList();

        actions.ShouldContain(ActivityAction.PasswordResetRequested);
        actions.ShouldContain(ActivityAction.PasswordResetCompleted);
    }

    [Fact]
    public async Task Su_kien_xac_thuc_hien_ra_o_man_nhat_ky_he_thong_cua_admin()
    {
        var admin = await CreateSystemAdminAsync();

        // Toàn bộ giá trị của ADR-058 nằm ở dòng này: ghi được vào bảng là chưa đủ, nó phải
        // ĐỌC ĐƯỢC ở chỗ người kiểm toán thật sự nhìn. EntityType = "Employee" nên nhóm này
        // đi lọt qua whitelist SystemScopedEntityTypes mà không cần nới whitelist đó.
        var page = await admin.Client.GetFromJsonAsync<PagedResult<SystemAuditLogResponse>>(
            "/api/v1/admin/audit-logs?pageSize=100", TestJson.Options);

        page!.Items.ShouldContain(l =>
            l.ActorId == admin.EmployeeId && l.Action == ActivityAction.Registered);
        page.Items.ShouldContain(l =>
            l.ActorId == admin.EmployeeId && l.Action == ActivityAction.LoggedIn);
    }

    private static string Sha256(string raw)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
