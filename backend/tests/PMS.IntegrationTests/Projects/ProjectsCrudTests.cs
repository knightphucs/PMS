using System.Net;
using System.Net.Http.Json;
using PMS.Application.Common.Models;
using PMS.Application.Features.Projects;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Projects;

[Collection(IntegrationTestCollection.Name)]
public class ProjectsCrudTests : IntegrationTestBase
{
    public ProjectsCrudTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact] // KB1 + KB3
    public async Task Tao_project_thi_nguoi_tao_thanh_ProjectManager_da_Accepted()
    {
        var a = await CreateUserAsync();

        var res = await a.Client.PostAsJsonAsync("/api/v1/Projects",
            new CreateProjectRequest("PMS", "Đồ án", DateTime.UtcNow.AddDays(30)));

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        res.Headers.Location.ShouldNotBeNull();       // CreatedAtAction phải sinh Location

        var summary = await res.Content.ReadFromJsonAsync<ProjectSummaryResponse>();
        var detail = await a.Client.GetFromJsonAsync<ProjectDetailResponse>(
            $"/api/v1/Projects/{summary!.Id}");

        var member = detail!.Members.ShouldHaveSingleItem();
        member.EmployeeId.ShouldBe(a.EmployeeId);
        member.RoleInProject.ShouldBe(RoleInProject.ProjectManager);
        member.InvitationStatus.ShouldBe(InvitationStatus.Accepted);
        member.EmployeeName.ShouldNotBeNullOrWhiteSpace();   // ThenInclude(m => m.Employee) có hiệu lực
    }

    [Fact] // KB2
    public async Task GetMine_chi_tra_project_cua_chinh_minh()
    {
        var a = await CreateUserAsync();
        await CreateProjectAsync(a.Client);

        var paged = await a.Client.GetFromJsonAsync<PagedResult<ProjectSummaryResponse>>(
            "/api/v1/Projects");

        paged!.Items.Count.ShouldBe(1);
        paged.Page.ShouldBe(1);
        paged.PageSize.ShouldBe(20);
        paged.TotalCount.ShouldBe(1);
    }

    [Fact] // KB6
    public async Task PM_sua_duoc_project()
    {
        var a = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);
        var before = await a.Client.GetFromJsonAsync<ProjectDetailResponse>($"/api/v1/Projects/{id}");

        var res = await a.Client.PutAsJsonAsync($"/api/v1/Projects/{id}",
            new UpdateProjectRequest("  Tên đã đổi  ", "Mô tả mới", DateTime.UtcNow.AddDays(60), before!.RowVersion));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<ProjectDetailResponse>();
        detail!.Name.ShouldBe("Tên đã đổi");     // service có Trim()
    }

    [Fact]
    public async Task Sua_project_voi_RowVersion_cu_thi_bi_chan_409()
    {
        var a = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);
        var before = await a.Client.GetFromJsonAsync<ProjectDetailResponse>($"/api/v1/Projects/{id}");

        // Sửa lần 1 thành công -> RowVersion trên DB đã đổi
        await a.Client.PutAsJsonAsync($"/api/v1/Projects/{id}",
            new UpdateProjectRequest("Lần sửa 1", "Mô tả 1", DateTime.UtcNow.AddDays(60), before!.RowVersion));

        // Sửa lần 2 vẫn dùng RowVersion cũ (giả lập request đến sau khi đã có người khác sửa)
        var res = await a.Client.PutAsJsonAsync($"/api/v1/Projects/{id}",
            new UpdateProjectRequest("Lần sửa 2", "Mô tả 2", DateTime.UtcNow.AddDays(60), before.RowVersion));

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory] // KB9 + KB10
    [InlineData("", 30, "Name rỗng")]
    [InlineData("PMS", -1, "ngày ở quá khứ")]
    public async Task Request_khong_hop_le_bi_ValidationFilter_chan(
        string name, int dayOffset, string _)
    {
        var a = await CreateUserAsync();

        var res = await a.Client.PostAsJsonAsync("/api/v1/Projects",
            new CreateProjectRequest(name, "Mô tả", DateTime.UtcNow.AddDays(dayOffset)));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // ValidationFilter trả ValidationProblemDetails -> phải có key "errors"
        var body = await res.Content.ReadAsStringAsync();
        body.ShouldContain("errors");
    }

    [Fact] // KB11
    public async Task PageSize_bi_kep_o_100_khong_cho_client_tu_quyet()
    {
        var a = await CreateUserAsync();

        var paged = await a.Client.GetFromJsonAsync<PagedResult<ProjectSummaryResponse>>(
            "/api/v1/Projects?pageSize=1000");

        // Setter của PagedRequest tự kẹp -> chặn client xin trang khổng lồ làm ngợp DB
        // (OWASP API4:2023 Unrestricted Resource Consumption)
        paged!.PageSize.ShouldBe(100);
    }

    [Fact] // KB12
    public async Task Id_khong_phai_guid_bi_routing_loai_thanh_404()
    {
        var a = await CreateUserAsync();

        var res = await a.Client.GetAsync("/api/v1/Projects/khong-phai-guid");

        // Route constraint {id:guid} loại từ tầng routing, chưa vào model binding
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}