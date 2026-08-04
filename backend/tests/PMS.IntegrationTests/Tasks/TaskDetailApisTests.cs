using System.Net;
using System.Net.Http.Json;
using PMS.Application.Common.Models;
using PMS.Application.Features.ActivityLogs;
using PMS.Application.Features.Labels;
using PMS.Application.Features.TaskLinks;
using PMS.Application.Features.Tasks;
using PMS.Application.Features.Watchers;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

/// <summary>
/// Bốn nhóm API mở khóa màn chi tiết Task: Label, Watcher, TaskLink, ActivityLog đọc.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TaskDetailApisTests : IntegrationTestBase
{
    public TaskDetailApisTests(PmsWebApplicationFactory factory) : base(factory) { }

    // ⚠️ Nhãn là state TOÀN TIẾN TRÌNH và PmsWebApplicationFactory không reset DB giữa các
    // test, nên tên nhãn phải duy nhất mỗi lần gọi — nếu không unique index sẽ làm test
    // đụng nhau một cách ngẫu nhiên.
    private static string UniqueLabelName() => $"lbl-{Guid.NewGuid():N}"[..20];

    // ==================== Label ====================

    [Fact]
    public async Task Gan_nhan_vao_task_thi_nhan_hien_tren_ca_chi_tiet_lan_the_board()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var label = await CreateLabelAsync(pm.Client, "#2563EB");

        var attach = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/labels/{label.Id}", null);
        attach.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);
        detail!.Labels.ShouldContain(l => l.Id == label.Id && l.Color == "#2563EB");

        // Thẻ board cũng phải có nhãn — thiếu Include ở query board thì chip biến mất im lặng
        var board = await pm.Client.GetFromJsonAsync<BoardResponse>(
            $"/api/v1/projects/{projectId}/board", TestJson.Options);
        board!.Columns.SelectMany(c => c.Tasks).First(t => t.Id == taskId)
             .Labels.ShouldContain(l => l.Id == label.Id);
    }

    [Fact]
    public async Task Gan_nhan_hai_lan_la_idempotent_chu_khong_phai_409()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var label = await CreateLabelAsync(pm.Client);

        await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/labels/{label.Id}", null);
        var second = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/labels/{label.Id}", null);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var labels = await second.Content.ReadFromJsonAsync<List<LabelResponse>>(TestJson.Options);
        labels!.Count(l => l.Id == label.Id).ShouldBe(1);   // không nhân đôi
    }

    [Fact]
    public async Task Nhan_trung_ten_bi_chan_409()
    {
        var pm = await CreateUserAsync();
        var name = UniqueLabelName();

        (await pm.Client.PostAsJsonAsync("/api/v1/labels", new CreateLabelRequest(name, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await pm.Client.PostAsJsonAsync("/api/v1/labels", new CreateLabelRequest(name, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Sua_hoac_xoa_nhan_toan_cuc_chi_danh_cho_SystemAdmin()
    {
        // ADR-037: xóa nhãn 'urgent' là gỡ chip khỏi board của MỌI project — không PM nào
        // nên sở hữu một tác dụng phụ xuyên project.
        var user = await CreateUserAsync();
        var label = await CreateLabelAsync(user.Client);

        (await user.Client.PutAsJsonAsync($"/api/v1/labels/{label.Id}",
            new UpdateLabelRequest(UniqueLabelName(), "#111111"))).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await user.Client.DeleteAsync($"/api/v1/labels/{label.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        var admin = await CreateSystemAdminAsync();
        (await admin.Client.DeleteAsync($"/api/v1/labels/{label.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Viewer_khong_gan_duoc_nhan_nhung_van_doc_duoc()
    {
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        var label = await CreateLabelAsync(pm.Client);

        (await viewer.Client.PostAsync($"/api/v1/tasks/{taskId}/labels/{label.Id}", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await viewer.Client.GetAsync("/api/v1/labels")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ==================== Watcher ====================

    [Fact]
    public async Task Theo_doi_task_va_bo_theo_doi_deu_idempotent()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var first = await WatchAsync(pm.Client, taskId);
        first.IsWatching.ShouldBeTrue();
        first.WatcherCount.ShouldBe(1);

        // Bấm lần hai không phải vi phạm nghiệp vụ (tiền lệ Notification.MarkAsRead)
        var second = await WatchAsync(pm.Client, taskId);
        second.IsWatching.ShouldBeTrue();
        second.WatcherCount.ShouldBe(1);

        var off = await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}/watchers/me");
        (await off.Content.ReadFromJsonAsync<WatchStateResponse>(TestJson.Options))!
            .IsWatching.ShouldBeFalse();

        // Bỏ theo dõi khi vốn không theo dõi cũng không phải lỗi
        var offAgain = await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}/watchers/me");
        offAgain.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IsWatching_tra_dung_theo_TUNG_nguoi_goi()
    {
        // Giá trị phụ thuộc NGƯỜI HỎI nên mapper phải nhận employeeId. Thiếu Include
        // Watchers ở GetWithDetailsAsync thì nó luôn false — sai im lặng (ADR-036).
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

        await WatchAsync(pm.Client, taskId);

        (await GetDetailAsync(pm.Client, taskId)).IsWatching.ShouldBeTrue();
        (await GetDetailAsync(member.Client, taskId)).IsWatching.ShouldBeFalse();
    }

    [Fact]
    public async Task Watcher_co_CreatedAt_that_chu_khong_phai_nam_0001()
    {
        // Watcher KHÔNG phải BaseEntity nên ApplyAuditFields() bỏ qua nó và
        // WatcherConfiguration không có default value — service phải tự set (ADR-036).
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        await WatchAsync(pm.Client, taskId);

        var watchers = await pm.Client.GetFromJsonAsync<List<WatcherResponse>>(
            $"/api/v1/tasks/{taskId}/watchers", TestJson.Options);
        watchers!.Single().CreatedAt.Year.ShouldBeGreaterThan(2000);
    }

    [Fact]
    public async Task Viewer_van_theo_doi_duoc_task()
    {
        // Thao tác ghi DUY NHẤT Viewer làm được: nó chỉ ảnh hưởng hộp thông báo của chính
        // họ, không ai khác nhìn thấy (ADR-036).
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        (await WatchAsync(viewer.Client, taskId)).IsWatching.ShouldBeTrue();
    }

    // ==================== TaskLink ====================

    [Fact]
    public async Task Lien_ket_hai_task_va_doc_nguoc_huong_tu_dau_kia()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var a = await CreateTaskAsync(pm.Client, projectId, "Task A");
        var b = await CreateTaskAsync(pm.Client, projectId, "Task B");

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{a}/links",
            new CreateTaskLinkRequest(b, LinkType.Blocks));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);

        var fromA = await GetLinksAsync(pm.Client, a);
        fromA.Single().LinkType.ShouldBe(LinkType.Blocks);
        fromA.Single().RelatedTaskId.ShouldBe(b);

        // Cùng một hàng, đọc từ B phải ra chiều ngược lại
        var fromB = await GetLinksAsync(pm.Client, b);
        fromB.Single().LinkType.ShouldBe(LinkType.IsBlockedBy);
        fromB.Single().RelatedTaskId.ShouldBe(a);
        fromB.Single().RelatedTaskCode.ShouldContain("-");   // mã đã ghép sẵn
    }

    [Fact]
    public async Task IsBlockedBy_duoc_chuan_hoa_nen_trung_ngu_nghia_bi_chan_409()
    {
        // Không chuẩn hóa thì Blocks(A,B) và IsBlockedBy(B,A) lọt qua unique index vì khác
        // giá trị cột, dù là CÙNG một sự thật (ADR-038).
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var a = await CreateTaskAsync(pm.Client, projectId, "Task A");
        var b = await CreateTaskAsync(pm.Client, projectId, "Task B");

        (await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{a}/links",
            new CreateTaskLinkRequest(b, LinkType.Blocks))).StatusCode
            .ShouldBe(HttpStatusCode.Created);

        (await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{b}/links",
            new CreateTaskLinkRequest(a, LinkType.IsBlockedBy))).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Vong_chan_bi_chan_409_ke_ca_khi_gian_tiep()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var a = await CreateTaskAsync(pm.Client, projectId, "A");
        var b = await CreateTaskAsync(pm.Client, projectId, "B");
        var c = await CreateTaskAsync(pm.Client, projectId, "C");

        await LinkAsync(pm.Client, a, b, LinkType.Blocks);
        await LinkAsync(pm.Client, b, c, LinkType.Blocks);

        // C chặn A sẽ khóa chết cả ba: không task nào vào được InProgress
        (await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{c}/links",
            new CreateTaskLinkRequest(a, LinkType.Blocks))).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Khong_lien_ket_duoc_voi_chinh_no_hoac_voi_task_project_khac()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "Dự án A");
        var projectB = await CreateProjectAsync(pm.Client, "Dự án B");
        var a = await CreateTaskAsync(pm.Client, projectA);
        var b = await CreateTaskAsync(pm.Client, projectB);

        (await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{a}/links",
            new CreateTaskLinkRequest(a, LinkType.RelatesTo))).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);

        (await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{a}/links",
            new CreateTaskLinkRequest(b, LinkType.RelatesTo))).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    // ==================== ActivityLog đọc ====================

    [Fact]
    public async Task Lich_su_task_ghi_lai_tao_va_doi_trang_thai()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        await AdvanceStatusAsync(pm.Client, taskId, Status.InProgress);

        var log = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/tasks/{taskId}/activity", TestJson.Options);

        log!.Items.ShouldContain(x => x.Action == ActivityAction.Created);
        log.Items.ShouldContain(x => x.Action == ActivityAction.StatusChanged);
        log.Items.ShouldAllBe(x => x.ActorId == pm.EmployeeId && x.ActorName.Length > 0);
    }

    [Fact]
    public async Task Lich_su_project_ghi_lai_ca_vong_doi_project_lan_hoat_dong_sprint()
    {
        // ProjectService trước 2026-08-03 KHÔNG ghi ActivityLog dòng nào — feed project sẽ
        // thiếu hẳn vòng đời của chính project. Test này khóa lại việc đó.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await CreateSprintAsync(pm.Client, projectId);

        var log = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/projects/{projectId}/activity", TestJson.Options);

        log!.Items.ShouldContain(x => x.Action == ActivityAction.Created);
        log.Items.Count.ShouldBeGreaterThanOrEqualTo(2);   // tạo project + tạo sprint
    }

    [Fact]
    public async Task Nguoi_ngoai_project_doc_lich_su_thi_nhan_404_chu_khong_phai_403()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await outsider.Client.GetAsync($"/api/v1/tasks/{taskId}/activity")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/activity")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    private static async Task<LabelResponse> CreateLabelAsync(HttpClient client, string? color = null)
    {
        var res = await client.PostAsJsonAsync("/api/v1/labels",
            new CreateLabelRequest(UniqueLabelName(), color));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<LabelResponse>(TestJson.Options))!;
    }

    private static async Task<WatchStateResponse> WatchAsync(HttpClient client, Guid taskId)
    {
        var res = await client.PostAsync($"/api/v1/tasks/{taskId}/watchers/me", null);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<WatchStateResponse>(TestJson.Options))!;
    }

    private static async Task<TaskDetailResponse> GetDetailAsync(HttpClient client, Guid taskId)
        => (await client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options))!;

    private static async Task<List<TaskLinkResponse>> GetLinksAsync(HttpClient client, Guid taskId)
        => (await client.GetFromJsonAsync<List<TaskLinkResponse>>(
            $"/api/v1/tasks/{taskId}/links", TestJson.Options))!;

    private static async Task LinkAsync(HttpClient client, Guid from, Guid to, LinkType type)
        => (await client.PostAsJsonAsync($"/api/v1/tasks/{from}/links",
            new CreateTaskLinkRequest(to, type))).StatusCode.ShouldBe(HttpStatusCode.Created);
}
