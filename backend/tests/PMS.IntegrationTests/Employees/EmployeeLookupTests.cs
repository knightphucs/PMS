using System.Net;
using System.Net.Http.Json;
using PMS.Application.Features.Employees;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Employees;

/// <summary>
/// <c>GET /employees?search=</c> — thêm 2026-08-04 để mời thành viên không phải gõ đúng
/// email bằng tay.
///
/// <para>
/// 🔴 Endpoint này mở cho MỌI người dùng đã đăng nhập, nên hai ràng buộc dưới đây không phải
/// chi tiết cài đặt mà là **lý do nó được phép tồn tại**: từ khóa ≥ 2 ký tự và trần kết quả
/// cứng ở server. Bỏ một trong hai là biến nó thành API dump toàn bộ danh bạ công ty.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class EmployeeLookupTests : IntegrationTestBase
{
    public EmployeeLookupTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Nguoi_dung_thuong_tra_duoc_nhan_su()
    {
        var me = await CreateUserAsync();
        var target = await CreateUserAsync();

        // Tra bằng một phần email — đủ dài để chỉ khớp đúng người này.
        var keyword = target.Email.Split('@')[0];

        var found = await me.Client.GetFromJsonAsync<List<EmployeeLookupResponse>>(
            $"/api/v1/employees?search={Uri.EscapeDataString(keyword)}", TestJson.Options);

        found.ShouldNotBeNull();
        found.ShouldContain(e => e.Id == target.EmployeeId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public async Task Tu_khoa_ngan_hon_2_ky_tu_tra_400_chu_khong_phai_danh_sach_rong(string keyword)
    {
        // 400 chứ không phải rỗng: rỗng khiến người dùng tưởng "không có ai tên vậy", trong
        // khi thật ra họ mới gõ chưa đủ. Và quan trọng hơn — một ký tự đơn lẻ khớp phần lớn
        // danh bạ, lặp 26 lần là có toàn bộ.
        var me = await CreateUserAsync();

        (await me.Client.GetAsync($"/api/v1/employees?search={keyword}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Khong_co_tham_so_search_cung_tra_400()
    {
        var me = await CreateUserAsync();

        (await me.Client.GetAsync("/api/v1/employees"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ket_qua_co_tran_cung_o_server()
    {
        // Mọi tài khoản test đều có email dạng `user-{guid}@pms.test`, nên "pms.test" khớp
        // rất nhiều người — đủ để chạm trần.
        var me = await CreateUserAsync();
        for (var i = 0; i < 12; i++) await CreateUserAsync();

        var found = await me.Client.GetFromJsonAsync<List<EmployeeLookupResponse>>(
            "/api/v1/employees?search=pms.test", TestJson.Options);

        found!.Count.ShouldBeLessThanOrEqualTo(10,
            "Trần kết quả phải cứng ở server; client không được tự chọn số lượng.");
    }

    [Fact]
    public async Task Khong_tra_ve_tai_khoan_da_bi_khoa()
    {
        // Mời một tài khoản đã khóa vào dự án tạo ra một thành viên không bao giờ đăng nhập
        // được — vô nghĩa, nên nó không nên xuất hiện trong ô gợi ý.
        var me = await CreateUserAsync();
        var admin = await CreateSystemAdminAsync();
        var victim = await CreateUserAsync();

        var keyword = victim.Email.Split('@')[0];

        (await admin.Client.PostAsJsonAsync(
            $"/api/v1/admin/employees/{victim.EmployeeId}/lock",
            new { Reason = "Kiểm thử ô gợi ý nhân sự" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var found = await me.Client.GetFromJsonAsync<List<EmployeeLookupResponse>>(
            $"/api/v1/employees?search={Uri.EscapeDataString(keyword)}", TestJson.Options);

        found!.ShouldNotContain(e => e.Id == victim.EmployeeId);
    }

    [Fact]
    public async Task Chi_tra_ve_ba_truong_khong_lo_vai_tro_hay_trang_thai_khoa()
    {
        // Hợp đồng của DTO: mỗi trường thêm vào là một mẩu thông tin nhân sự phát cho toàn
        // công ty. Khẳng định trên JSON THÔ, vì deserialize vào record sẽ âm thầm bỏ qua
        // những trường thừa và test vẫn xanh.
        var me = await CreateUserAsync();
        var target = await CreateUserAsync();
        var keyword = target.Email.Split('@')[0];

        var raw = await me.Client.GetStringAsync(
            $"/api/v1/employees?search={Uri.EscapeDataString(keyword)}");

        raw.ShouldNotContain("systemRole", Case.Insensitive);
        raw.ShouldNotContain("isLocked", Case.Insensitive);
        raw.ShouldNotContain("lockReason", Case.Insensitive);
        raw.ShouldNotContain("createdAt", Case.Insensitive);
    }
}
