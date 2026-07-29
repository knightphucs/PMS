using System.Net;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace PMS.IntegrationTests.Projects;

public class HarnessSmokeTests : IntegrationTestBase
{
    public HarnessSmokeTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task App_khoi_dong_va_chan_request_khong_co_token()
    {
        var client = Factory.CreateClient();
        var res = await client.GetAsync($"/api/v1/Projects/{Guid.NewGuid()}");
        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dang_ky_va_goi_endpoint_can_auth_thanh_cong()
    {
        var client = await CreateAuthenticatedClientAsync();
        var res = await client.GetAsync("/api/v1/Projects");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_endpoint_khong_can_token_va_khong_lo_thong_tin_ha_tang()
    {
        var res = await Factory.CreateClient().GetAsync("/health");

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.ShouldContain("Healthy");
        body.ShouldNotContain("Server="); 
        body.ShouldNotContain("Password");
    }
}