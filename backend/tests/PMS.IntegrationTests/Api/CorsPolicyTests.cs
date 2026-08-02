using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Api;

/// <summary>
/// Giữ quyết định "chuẩn hóa CORS Policy" (§15, 2026-07-29) khỏi bị vô hiệu hóa im lặng.
/// Trước phiên này Program.cs gọi app.UseCors() KHÔNG tham số trong khi chỉ có policy đặt
/// tên "PmsFrontend" và không có AddDefaultPolicy — CorsMiddleware không tìm thấy default
/// policy thì chỉ log rồi đi tiếp, nên không header CORS nào được phát ra mà build vẫn
/// sạch và không test nào đỏ. Cùng lớp lỗi với bài học ADR-008/ADR-016: tài liệu ghi ✅
/// nhưng code im lặng không làm gì.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CorsPolicyTests : IntegrationTestBase
{
    public CorsPolicyTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Request_tu_origin_duoc_cau_hinh_nhan_duoc_header_Access_Control_Allow_Origin()
    {
        var client = Factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", PmsWebApplicationFactory.TestFrontendOrigin);

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            .ShouldBeTrue("Không có header CORS — policy 'PmsFrontend' chưa được gắn vào pipeline.");
        values!.ShouldContain(PmsWebApplicationFactory.TestFrontendOrigin);
    }

    [Fact]
    public async Task Preflight_tu_origin_duoc_cau_hinh_duoc_phep_goi_POST()
    {
        var client = Factory.CreateClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/Auth/login");
        preflight.Headers.Add("Origin", PmsWebApplicationFactory.TestFrontendOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(preflight);

        response.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods).ShouldBeTrue();
        methods!.ShouldContain(m => m.Contains("POST"));
    }

    [Fact]
    public async Task Request_tu_origin_la_khong_nhan_duoc_header_CORS()
    {
        // AllowAnyOrigin bị cấm khi có JWT (§7) — origin ngoài danh sách phải không được cấp header.
        var client = Factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://evil.example.com");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    /// <summary>Header Authorization phải đi qua được — AllowAnyHeader() trong policy.</summary>
    [Fact]
    public async Task Preflight_cho_phep_header_Authorization()
    {
        var client = Factory.CreateClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/Projects");
        preflight.Headers.Add("Origin", PmsWebApplicationFactory.TestFrontendOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization");

        var response = await client.SendAsync(preflight);

        response.Headers.TryGetValues("Access-Control-Allow-Headers", out var headers).ShouldBeTrue();
        headers!.ShouldContain(h => h.Contains("authorization", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ADR-027: thiếu header này thì trình duyệt vứt bỏ Set-Cookie ở phản hồi cross-origin
    /// và không đính cookie vào request sau — toàn bộ luồng refresh hỏng im lặng, đúng kiểu
    /// hỏng mà chính lớp test này sinh ra để chặn.
    /// </summary>
    [Fact]
    public async Task Phan_hoi_cho_phep_gui_kem_cookie_credentials()
    {
        var client = Factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", PmsWebApplicationFactory.TestFrontendOrigin);

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var values)
            .ShouldBeTrue("Thiếu Access-Control-Allow-Credentials — cookie refresh của ADR-027 sẽ không hoạt động.");
        values!.ShouldContain("true");
    }
}
