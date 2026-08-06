using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.BoardColumns;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

[Collection(IntegrationTestCollection.Name)]
public class TasksCrudTests : IntegrationTestBase
{
    public TasksCrudTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Tao_task_thi_nguoi_tao_thanh_Reporter_va_task_bat_dau_o_ToDo()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var taskId = await CreateTaskAsync(pm.Client, projectId, "Dựng API");

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{taskId}", TestJson.Options);
        detail!.Status.Name.ShouldBe("Cần làm");
        detail.ReporterId.ShouldBe(pm.EmployeeId);
        detail.ProjectId.ShouldBe(projectId);
        detail.Assignees.ShouldBeEmpty();
        detail.RowVersion.ShouldNotBeEmpty();
    }

    // ---------- Bấm "+" trên một cột cụ thể (2026-08-06) ----------

    [Fact]
    public async Task Tao_task_voi_BoardColumnId_thi_vao_dung_cot_do_khong_phai_cot_trai_nhat()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var columns = await pm.Client.GetFromJsonAsync<List<BoardColumnResponse>>(
            $"/api/v1/projects/{projectId}/columns", TestJson.Options);
        var targetColumn = columns!.Single(c => c.Order == 2);   // KHÔNG phải cột trái nhất (Order 0)

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Việc dở dang", projectId, null, null, null, Priority.Medium,
                BoardColumnId: targetColumn.Id));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await res.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options);
        body!.Status.ColumnId.ShouldBe(targetColumn.Id);
    }

    [Fact]
    public async Task Tao_task_khong_truyen_BoardColumnId_van_vao_cot_trai_nhat_nhu_truoc()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var columns = await pm.Client.GetFromJsonAsync<List<BoardColumnResponse>>(
            $"/api/v1/projects/{projectId}/columns", TestJson.Options);
        var leftmost = columns!.Single(c => c.Order == 0);

        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);

        detail!.Status.ColumnId.ShouldBe(leftmost.Id);
    }

    [Fact]
    public async Task Tao_task_voi_BoardColumnId_thuoc_project_khac_tra_404()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "Project A");
        var projectB = await CreateProjectAsync(pm.Client, "Project B");

        var columnsB = await pm.Client.GetFromJsonAsync<List<BoardColumnResponse>>(
            $"/api/v1/projects/{projectB}/columns", TestJson.Options);

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Việc lạc chỗ", projectA, null, null, null, Priority.Medium,
                BoardColumnId: columnsB!.First().Id));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- ADR-033/034: mã task PMS-12 ----------

    [Fact]
    public async Task Ma_task_danh_so_tang_dan_va_dat_lai_tu_dau_o_moi_project()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "Hệ thống kho");
        var projectB = await CreateProjectAsync(pm.Client, "Website bán hàng");

        var a1 = await GetSummaryAsync(pm.Client, await CreateTaskAsync(pm.Client, projectA, "Task A1"));
        var a2 = await GetSummaryAsync(pm.Client, await CreateTaskAsync(pm.Client, projectA, "Task A2"));
        var b1 = await GetSummaryAsync(pm.Client, await CreateTaskAsync(pm.Client, projectB, "Task B1"));

        a1.Number.ShouldBe(1);
        a2.Number.ShouldBe(2);
        b1.Number.ShouldBe(1);   // hai project đánh số ĐỘC LẬP

        // Mã ghép sẵn ở backend, không bắt frontend tự nối (ADR-034)
        a2.Code.ShouldBe($"{a2.ProjectKey}-2");
        a1.ProjectKey.ShouldNotBe(b1.ProjectKey);   // mã project duy nhất toàn hệ thống
    }

    [Fact]
    public async Task Ma_project_sinh_tu_ten_va_bo_dau_tieng_Viet()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, $"Hệ thống kho {Guid.NewGuid():N}");
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var detail = await GetSummaryAsync(pm.Client, taskId);

        // "Hệ thống kho ..." -> chữ cái đầu mỗi từ, đã bóc dấu: H, T, K (+ hậu tố nếu trùng)
        detail.ProjectKey.ShouldStartWith("HTK");
        detail.ProjectKey.ShouldBe(detail.ProjectKey.ToUpperInvariant());
    }

    [Fact]
    public async Task So_task_khong_tai_su_dung_sau_khi_task_bi_xoa_mem()
    {
        // Mã PMS-12 đã phát tán ra comment/URL/tài liệu ngoài — cấp lại số đó cho task
        // khác là làm sai lệch mọi tham chiếu cũ (ADR-033).
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var firstId = await CreateTaskAsync(pm.Client, projectId, "Task sẽ bị xóa");
        (await GetSummaryAsync(pm.Client, firstId)).Number.ShouldBe(1);

        (await pm.Client.DeleteAsync($"/api/v1/tasks/{firstId}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var secondId = await CreateTaskAsync(pm.Client, projectId, "Task mới");
        (await GetSummaryAsync(pm.Client, secondId)).Number.ShouldBe(2);   // KHÔNG phải 1
    }

    [Fact]
    public async Task Description_luu_va_tra_ve_dung_qua_ca_tao_lan_sua()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Task có mô tả", projectId, null, null, null,
                Priority.Medium, "Mô tả chi tiết công việc"));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await res.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options);

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{created!.Id}", TestJson.Options);
        detail!.Description.ShouldBe("Mô tả chi tiết công việc");

        var updated = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{created.Id}",
            new UpdateTaskRequest("Task có mô tả", null, Priority.Medium,
                detail.RowVersion, "Mô tả đã sửa"));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await updated.Content.ReadFromJsonAsync<TaskDetailResponse>(TestJson.Options);
        after!.Description.ShouldBe("Mô tả đã sửa");
    }

    /// <summary>Lấy chi tiết task rồi rút gọn về đúng ba trường mà nhóm test mã task quan tâm.</summary>
    private static async Task<(int Number, string Code, string ProjectKey)> GetSummaryAsync(
        HttpClient client, Guid taskId)
    {
        var detail = await client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);
        return (detail!.Number, detail.Code, detail.ProjectKey);
    }

    [Fact]
    public async Task Sua_task_voi_RowVersion_cu_thi_bi_chan_409()
    {
        // ADR-016/021: chỉ có cột RowVersion là chưa đủ, client phải round-trip đúng token.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{taskId}", TestJson.Options);
        var staleRowVersion = detail!.RowVersion;

        var first = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Lần sửa 1", null, Priority.High, staleRowVersion));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Lần sửa 2", null, Priority.Low, staleRowVersion));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var after = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{taskId}", TestJson.Options);
        after!.Name.ShouldBe("Lần sửa 1");   // lần 2 không được ghi đè
    }

    [Fact]
    public async Task Sua_task_thieu_RowVersion_bi_ValidationFilter_chan_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Tên mới", null, Priority.High, []));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Xoa_task_khong_co_subtask_thanh_cong_va_chi_xoa_mem()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        (await pm.Client.GetAsync($"/api/v1/tasks/{taskId}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        var stillInDb = await WithDbAsync(db => db.Tasks
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == taskId && t.IsDeleted));
        stillInDb.ShouldBeTrue();
    }

    [Fact]
    public async Task Danh_sach_task_cua_project_khong_liet_ke_subtask()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        await CreateTaskAsync(pm.Client, projectId, "Subtask", parentTaskId: parentId);

        var paged = await pm.Client.GetFromJsonAsync<PagedResult<TaskSummaryResponse>>(
            $"/api/v1/projects/{projectId}/tasks", TestJson.Options);

        // Subtask hiện trong chi tiết task cha, không phải mục riêng ở danh sách
        paged!.Items.ShouldHaveSingleItem().Id.ShouldBe(parentId);
        paged.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Tao_task_ghi_ActivityLog()
    {
        // ADR-013 chấp nhận rủi ro "có thể quên gọi logger" -> mỗi action phải có test
        // khẳng định số dòng ActivityLogs tăng đúng.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await CountActivityLogsAsync(taskId)).ShouldBe(1);
    }
}
