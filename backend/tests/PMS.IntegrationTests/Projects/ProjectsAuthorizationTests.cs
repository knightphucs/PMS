using System.Net;
using System.Net.Http.Json;
using PMS.Application.Common.Models;
using PMS.Application.Features.Projects;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Projects;

[Collection(IntegrationTestCollection.Name)]
public class ProjectsAuthorizationTests : IntegrationTestBase
{
    public ProjectsAuthorizationTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact] // KB4 — kịch bản quan trọng nhất
    public async Task Nguoi_ngoai_project_nhan_404_chu_khong_phai_403()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);

        var res = await b.Client.GetAsync($"/api/v1/Projects/{id}");

        // ADR-006: 403 sẽ xác nhận project TỒN TẠI -> rò rỉ thông tin cho người ngoài.
        // Đây là OWASP API1:2023 (BOLA). Phải là 404.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact] // KB5
    public async Task GetMine_khong_lo_project_cua_nguoi_khac()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        await CreateProjectAsync(a.Client);

        var paged = await b.Client.GetFromJsonAsync<PagedResult<ProjectSummaryResponse>>(
            "/api/v1/Projects", TestJson.Options);

        paged!.Items.ShouldBeEmpty();
        paged.TotalCount.ShouldBe(0);
    }

    [Fact] // KB7
    public async Task Nguoi_ngoai_project_sua_project_nhan_404()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);

        var res = await b.Client.PutAsJsonAsync($"/api/v1/Projects/{id}",
            new UpdateProjectRequest("Hack", "Hack", DateTime.UtcNow.AddDays(10), [1]));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact] // KB8
    public async Task Khong_co_token_nhan_401()
    {
        var a = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);

        var anonymous = Factory.CreateClient();
        var res = await anonymous.GetAsync($"/api/v1/Projects/{id}");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact] // KB17
    public async Task Member_sua_project_nhan_403_chu_khong_phai_404()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);
        await SeedMemberAsync(id, b.EmployeeId, RoleInProject.Member, InvitationStatus.Accepted);

        // B là thành viên -> View được
        (await b.Client.GetAsync($"/api/v1/Projects/{id}")).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        // nhưng Update thì không -> 403, KHÁC với 404 của người ngoài
        var res = await b.Client.PutAsJsonAsync($"/api/v1/Projects/{id}",
            new UpdateProjectRequest("Đổi", "Đổi", DateTime.UtcNow.AddDays(10), [1]));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact] // KB18 — bổ sung, không có trong danh sách gốc
    public async Task Thanh_vien_dang_Pending_chua_co_quyen_gi()
    {
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);
        await SeedMemberAsync(id, b.EmployeeId, RoleInProject.ProjectManager,
                              InvitationStatus.Pending);   // PM nhưng CHƯA Accepted

        var res = await b.Client.GetAsync($"/api/v1/Projects/{id}");

        // "Được mời" ≠ "có quyền". GetRoleInProjectAsync lọc InvitationStatus == Accepted.
        // Nếu quên điều kiện đó, người mới được mời sẽ có ngay quyền PM.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}