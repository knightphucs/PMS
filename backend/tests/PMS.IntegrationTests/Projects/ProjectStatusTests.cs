using System.Net;
using System.Net.Http.Json;
using PMS.Application.Features.Projects;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Projects;

/// <summary>
/// <c>POST /projects/{id}/complete</c> và <c>/reopen</c> — thêm 2026-08-04.
/// <para>
/// Trước đó `Project.Complete()` có đúng MỘT caller trong toàn bộ solution (`DbSeeder`), nên
/// mọi project tạo qua API vĩnh viễn nằm ở `ToDo` trong khi `Status` vẫn được trả trong DTO
/// và vẫn là khóa `sortBy` hợp lệ — một trường chết đội lốt tính năng.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ProjectStatusTests : IntegrationTestBase
{
    public ProjectStatusTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PM_danh_dau_hoan_thanh_va_mo_lai_duoc()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var done = await PostAsync(pm.Client, projectId, "complete");
        done.Status.ShouldBe(Status.Done);

        // Mở lại về InProgress, KHÔNG về ToDo: project từng chạy tới Done thì công việc đã
        // diễn ra, quay về "chưa bắt đầu" là ghi lại một điều không đúng sự thật.
        var reopened = await PostAsync(pm.Client, projectId, "reopen");
        reopened.Status.ShouldBe(Status.InProgress);
    }

    [Fact]
    public async Task Hoan_thanh_hai_lan_la_idempotent()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await PostAsync(pm.Client, projectId, "complete");

        // Gọi lại không phải lỗi nghiệp vụ, chỉ là không có gì để làm.
        var again = await PostAsync(pm.Client, projectId, "complete");
        again.Status.ShouldBe(Status.Done);
    }

    [Fact]
    public async Task Mo_lai_project_chua_hoan_thanh_tra_409()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        (await pm.Client.PostAsync($"/api/v1/projects/{projectId}/reopen", null))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Member_va_Viewer_khong_doi_duoc_trang_thai()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var member = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        foreach (var client in new[] { member.Client, viewer.Client })
            (await client.PostAsync($"/api/v1/projects/{projectId}/complete", null))
                .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        (await outsider.Client.PostAsync($"/api/v1/projects/{projectId}/complete", null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Doi_trang_thai_bao_cho_thanh_vien_khac_chu_khong_bao_cho_chinh_minh()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var member = await CreateUserAsync();
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

        await PostAsync(pm.Client, projectId, "complete");

        var mine = await CountNotificationsAsync(member.Client, NotificationType.ProjectStatusChanged);
        mine.ShouldBe(1);

        // 🔴 `ProjectStatusChanged` chứ KHÔNG phải `StatusChanged`: RelatedEntityKind được
        // SUY RA từ Type (ADR-025), và `StatusChanged` suy ra `Task`. Dùng nhầm sẽ khiến
        // chuông điều hướng tới /tasks/{projectId} — một id task không tồn tại.
        var actorGot = await CountNotificationsAsync(pm.Client, NotificationType.ProjectStatusChanged);
        actorGot.ShouldBe(0, "Người tự tay đổi trạng thái không cần được báo về việc mình vừa làm.");
    }

    // ---------- helper ----------

    private static async Task<ProjectDetailResponse> PostAsync(
        HttpClient client, Guid projectId, string action)
    {
        var res = await client.PostAsync($"/api/v1/projects/{projectId}/{action}", null);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ProjectDetailResponse>(TestJson.Options))!;
    }

    private static async Task<int> CountNotificationsAsync(HttpClient client, NotificationType type)
    {
        var page = await client.GetFromJsonAsync<
            Application.Common.Models.PagedResult<Application.Features.Notifications.NotificationResponse>>(
            "/api/v1/notifications?pageSize=100", TestJson.Options);

        return page!.Items.Count(n => n.Type == type);
    }
}
