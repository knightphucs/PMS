using System.Net;
using System.Net.Http.Json;
using PMS.Application.Features.Statistics;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Statistics;

/// <summary>
/// <c>GET /projects/{id}/statistics</c> — trước 2026-08-04 <b>không có file test nào</b> nhắc
/// tới <c>StatisticsService</c>, dù nó là endpoint duy nhất trong hệ thống làm phép tổng hợp
/// (zero-fill, tỷ lệ hoàn thành, khối lượng theo người). Đây đúng loại code mà "build sạch,
/// test xanh" không nói gì về tính đúng đắn: một phép chia sai vẫn build được.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class StatisticsTests : IntegrationTestBase
{
    public StatisticsTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Project_rong_tra_ve_ty_le_0_chu_khong_chia_cho_0()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var stats = await pm.Client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/v1/projects/{projectId}/statistics", TestJson.Options);

        stats!.TotalTasks.ShouldBe(0);
        stats.OverdueTasks.ShouldBe(0);
        stats.CompletionRate.ShouldBe(0m);
    }

    [Fact]
    public async Task Zero_fill_tra_du_moi_gia_tri_enum_ke_ca_khi_khong_co_task_nao()
    {
        // 🔴 Hợp đồng mà frontend dựa vào để KHÔNG phải tự vá cột thiếu khi vẽ biểu đồ.
        // Mất nó thì biểu đồ nhảy số cột theo dữ liệu và trục X đổi hình mỗi lần tải.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var stats = await pm.Client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/v1/projects/{projectId}/statistics", TestJson.Options);

        // ADR-052: ByStatus nay là DANH SÁCH CỘT của project (bốn cột mặc định), không
        // còn là bốn giá trị enum. Cột rỗng vẫn phải có mặt với số 0 — biểu đồ thiếu cột
        // khiến người đọc tưởng cột đó không tồn tại chứ không phải đang trống.
        stats!.ByStatus.Select(s => s.Name)
             .ShouldBe(["Cần làm", "Đang làm", "Đang duyệt", "Hoàn thành"]);
        stats.ByStatus.ShouldAllBe(s => s.Count == 0);
        stats.ByPriority.Select(p => p.Priority)
             .ShouldBe(Enum.GetValues<Priority>(), ignoreOrder: true);
    }

    [Fact]
    public async Task Ty_le_hoan_thanh_tinh_dung_va_dem_ca_subtask()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        // 4 task: 1 đưa tới Done, 3 để ToDo -> 25%
        var done = await CreateTaskAsync(pm.Client, projectId);
        await CreateTaskAsync(pm.Client, projectId);
        await CreateTaskAsync(pm.Client, projectId);
        await CreateTaskAsync(pm.Client, projectId);

        await MoveToColumnAsync(pm.Client, done, 3);

        var stats = await pm.Client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/v1/projects/{projectId}/statistics", TestJson.Options);

        stats!.TotalTasks.ShouldBe(4);
        stats.CompletionRate.ShouldBe(25m);
        stats.ByStatus.Single(s => s.Name == "Hoàn thành").Count.ShouldBe(1);
        stats.ByStatus.Single(s => s.Name == "Cần làm").Count.ShouldBe(3);
    }

    [Fact]
    public async Task Ca_ba_vai_tro_deu_xem_duoc_thong_ke()
    {
        // ADR-039: Member được thêm vào 2026-08-03. Thống kê là tổng hợp của dữ liệu mà
        // Member vốn đã đọc được qua /board, nên nó không phải một đặc quyền.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var member = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        foreach (var client in new[] { pm.Client, member.Client, viewer.Client })
            (await client.GetAsync($"/api/v1/projects/{projectId}/statistics"))
                .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_chu_khong_phai_403()
    {
        // Giữ đúng hợp đồng ADR-006/019: 403 sẽ xác nhận project đó CÓ TỒN TẠI.
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        (await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/statistics"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
