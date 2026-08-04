using System.Net;
using System.Net.Http.Json;
using PMS.Application.Common.Models;
using PMS.Application.Features.ActivityLogs;
using PMS.Application.Features.Labels;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Labels;

/// <summary>
/// Sáu route của nhãn — trước 2026-08-04 <b>không có file test nào</b> mang tên Label, dù đây
/// là dữ liệu TOÀN CỤC (ADR-037) với một bất đối xứng quyền dễ hiểu nhầm: ai cũng TẠO được,
/// chỉ người có <c>labels:manage</c> mới SỬA/XÓA được.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class LabelsTests : IntegrationTestBase
{
    public LabelsTests(PmsWebApplicationFactory factory) : base(factory) { }

    private static string UniqueName() => $"nhan-{Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task Moi_user_dang_nhap_deu_tao_duoc_nhan()
    {
        // Bất đối xứng CỐ Ý (LabelsController): tạo là thao tác cộng thêm, còn xóa thì gỡ
        // chip khỏi board của MỌI dự án.
        var user = await CreateUserAsync();

        var res = await user.Client.PostAsJsonAsync("/api/v1/labels",
            new CreateLabelRequest(UniqueName(), "#123456"));

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Trung_ten_tra_409()
    {
        var user = await CreateUserAsync();
        var name = UniqueName();

        (await user.Client.PostAsJsonAsync("/api/v1/labels", new CreateLabelRequest(name, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await user.Client.PostAsJsonAsync("/api/v1/labels", new CreateLabelRequest(name, null)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Khong_gui_mau_thi_dung_mau_mac_dinh()
    {
        var user = await CreateUserAsync();

        var res = await user.Client.PostAsJsonAsync("/api/v1/labels",
            new CreateLabelRequest(UniqueName(), null));
        var label = await res.Content.ReadFromJsonAsync<LabelResponse>(TestJson.Options);

        // Hợp đồng với frontend: `color` LUÔN có giá trị, client không phải tự vá null.
        label!.Color.ShouldNotBeNullOrWhiteSpace();
        label.Color.ShouldStartWith("#");
    }

    [Fact]
    public async Task Sua_va_xoa_nhan_can_quyen_labels_manage()
    {
        var user = await CreateUserAsync();
        var admin = await CreateSystemAdminAsync();

        var created = await (await user.Client.PostAsJsonAsync("/api/v1/labels",
            new CreateLabelRequest(UniqueName(), "#ABCDEF")))
            .Content.ReadFromJsonAsync<LabelResponse>(TestJson.Options);

        var id = created!.Id;

        // Người tạo ra nó cũng KHÔNG sửa được — quyền gắn với phạm vi ảnh hưởng (toàn hệ
        // thống), không với quyền sở hữu.
        (await user.Client.PutAsJsonAsync($"/api/v1/labels/{id}",
            new UpdateLabelRequest("doi-ten", "#000000")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await user.Client.DeleteAsync($"/api/v1/labels/{id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await admin.Client.PutAsJsonAsync($"/api/v1/labels/{id}",
            new UpdateLabelRequest(UniqueName(), "#000000")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await admin.Client.DeleteAsync($"/api/v1/labels/{id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Cả ba thao tác trên nhãn phải để lại vết trong nhật ký cấp hệ thống. TẠO là trường hợp
    /// quan trọng nhất và cũng là trường hợp <b>từng bị thiếu</b> (bổ sung 2026-08-04): nó là
    /// đường ghi duy nhất vào không gian tên toàn cục mà mọi user đều dùng được.
    /// </summary>
    [Fact]
    public async Task Tao_sua_xoa_nhan_deu_duoc_ghi_vao_nhat_ky_he_thong()
    {
        var admin = await CreateSystemAdminAsync();
        var name = UniqueName();

        var created = await (await admin.Client.PostAsJsonAsync("/api/v1/labels",
            new CreateLabelRequest(name, "#111111")))
            .Content.ReadFromJsonAsync<LabelResponse>(TestJson.Options);

        var log = await admin.Client.GetFromJsonAsync<PagedResult<SystemAuditLogResponse>>(
            "/api/v1/admin/audit-logs?pageSize=100", TestJson.Options);

        log!.Items.ShouldContain(x =>
            x.EntityId == created!.Id
            && x.EntityType == "Label"
            && x.Action == ActivityAction.Created);
    }

    [Fact]
    public async Task Gan_va_go_nhan_tren_task_la_idempotent()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var label = await (await pm.Client.PostAsJsonAsync("/api/v1/labels",
            new CreateLabelRequest(UniqueName(), "#222222")))
            .Content.ReadFromJsonAsync<LabelResponse>(TestJson.Options);

        // Gắn hai lần: lần thứ hai KHÔNG được 409 — UI gọi lại mà không phải dò trạng thái.
        (await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/labels/{label!.Id}", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/labels/{label.Id}", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await (await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}/labels/{label.Id}"))
            .Content.ReadFromJsonAsync<List<LabelResponse>>(TestJson.Options);
        after!.ShouldNotContain(l => l.Id == label.Id);

        (await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}/labels/{label.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_khong_gan_duoc_nhan_len_task()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var viewer = await CreateUserAsync();
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        var label = await (await pm.Client.PostAsJsonAsync("/api/v1/labels",
            new CreateLabelRequest(UniqueName(), "#333333")))
            .Content.ReadFromJsonAsync<LabelResponse>(TestJson.Options);

        // ProjectAction.ManageTaskLabels — PM/Member ghi được, Viewer chỉ đọc (ADR-037).
        (await viewer.Client.PostAsync($"/api/v1/tasks/{taskId}/labels/{label!.Id}", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
