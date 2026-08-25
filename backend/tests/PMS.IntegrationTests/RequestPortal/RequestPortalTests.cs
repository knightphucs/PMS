using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.CustomFields;
using PMS.Application.Features.RequestPortal;
using PMS.Application.Features.WorkItemTypes;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.RequestPortal;

/// <summary>
/// Cổng yêu cầu (ADR-063) — đường để người <b>ngoài</b> project gửi việc vào.
///
/// <para>
/// 🔑 <b>Rủi ro của cụm này khác hẳn mọi cụm khác trong bộ test, và test bám đúng chỗ đó.</b>
/// Mọi feature trước ADR-063 đều nằm sau <c>ProjectAuthorizationService</c>, tức có một
/// chốt chặn dùng chung canh giúp. Cổng yêu cầu <b>cố ý không đi qua chốt đó</b> (xem XML
/// doc của <c>RequestPortalService</c>), nên nó không thừa hưởng bảo đảm nào — mỗi bảo đảm
/// phải được khẳng định ở đây bằng một test riêng.
/// </para>
/// <para>
/// Vì vậy phép kiểm quan trọng nhất của cả file là
/// <see cref="Cong_yeu_cau_KHONG_mo_them_cua_nao_khac"/>: nó khẳng định điều mà đường (a)
/// của ADR-063 (thêm <c>RoleInProject.Requester</c>) sẽ phá vỡ nếu ai đó quay lại chọn nó.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class RequestPortalTests : IntegrationTestBase
{
    public RequestPortalTests(PmsWebApplicationFactory factory) : base(factory) { }

    // ---------- helper ----------

    private static async Task<FieldDefinitionResponse> CreateFieldAsync(
        HttpClient client, Guid projectId, string label, FieldType type,
        IReadOnlyList<FieldOptionRequest>? options = null)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/fields",
            new CreateFieldDefinitionRequest(label, type, options));
        res.StatusCode.ShouldBe(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<FieldDefinitionResponse>(TestJson.Options))!;
    }

    private static async Task<WorkItemTypeResponse> CreateTypeAsync(
        HttpClient client, Guid projectId, string name,
        bool requestable, string? instructions = null,
        IReadOnlyList<WorkItemTypeFieldRequest>? fields = null)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/work-item-types",
            new CreateWorkItemTypeRequest(name, "KeyRound", "#7C3AED", fields,
                IsRequestable: requestable, RequestInstructions: instructions));
        res.StatusCode.ShouldBe(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<WorkItemTypeResponse>(TestJson.Options))!;
    }

    private static Task<HttpResponseMessage> SubmitAsync(
        HttpClient client, Guid projectId, Guid typeId, string name = "Xin cấp quyền",
        IReadOnlyList<SetFieldValueRequest>? values = null)
        => client.PostAsJsonAsync($"/api/v1/request-portal/projects/{projectId}/requests",
            new SubmitRequestRequest(typeId, name, "Mô tả", Priority.Medium, null, values));

    /// <summary>Một project có cổng mở + một người NGOÀI project đó.</summary>
    private async Task<(TestUser Pm, TestUser Outsider, Guid ProjectId, Guid TypeId)> SeedPortalAsync(
        string typeName = "Yêu cầu cấp quyền")
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, $"Hạ tầng {Guid.NewGuid():N}");
        var type = await CreateTypeAsync(pm.Client, projectId, typeName, requestable: true);
        return (pm, outsider, projectId, type.Id);
    }

    // ---------- G1: cờ IsRequestable phải CHẶN được ----------

    [Fact]
    public async Task Loai_KHONG_requestable_thi_khong_gui_duoc()
    {
        var (pm, outsider, projectId, _) = await SeedPortalAsync();
        var closed = await CreateTypeAsync(pm.Client, projectId, "Việc nội bộ", requestable: false);

        var res = await SubmitAsync(outsider.Client, projectId, closed.Id);

        // 404 chứ không 403: không xác nhận id đó có tồn tại.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Loai_cua_project_KHAC_thi_khong_gui_duoc()
    {
        var a = await SeedPortalAsync("Loại A");
        var b = await SeedPortalAsync("Loại B");

        // Gửi vào project A nhưng mang id loại của project B — ranh giới chéo project.
        var res = await SubmitAsync(a.Outsider.Client, a.ProjectId, b.TypeId);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Project_chua_mo_cong_thi_khong_co_form()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, "Không mở cổng");

        var res = await outsider.Client.GetAsync($"/api/v1/request-portal/projects/{projectId}/form");

        // 404 chứ không phải một form rỗng: form rỗng vẫn xác nhận project này tồn tại.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- G2: IsRequired cưỡng chế LÚC GỬI ----------

    [Fact]
    public async Task Thieu_truong_bat_buoc_thi_bi_chan_kem_thong_diep_doc_duoc()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, "Hạ tầng");
        var field = await CreateFieldAsync(pm.Client, projectId, "Lý do", FieldType.Text);
        var type = await CreateTypeAsync(pm.Client, projectId, "Yêu cầu", requestable: true,
            fields: [new WorkItemTypeFieldRequest(field.Id, IsRequired: true)]);

        var res = await SubmitAsync(outsider.Client, projectId, type.Id);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // Thông điệp phải nêu TÊN trường — "thiếu trường bắt buộc" là thứ người dùng không
        // sửa được, họ không biết ô nào.
        (await res.Content.ReadAsStringAsync()).ShouldContain("Lý do");
    }

    [Fact]
    public async Task Truong_KHONG_bat_buoc_de_trong_thi_van_gui_duoc()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, "Hạ tầng");
        var field = await CreateFieldAsync(pm.Client, projectId, "Ghi chú", FieldType.Text);
        var type = await CreateTypeAsync(pm.Client, projectId, "Yêu cầu", requestable: true,
            fields: [new WorkItemTypeFieldRequest(field.Id, IsRequired: false)]);

        var res = await SubmitAsync(outsider.Client, projectId, type.Id);

        res.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Và KHÔNG tạo hàng FieldValue rỗng — ADR-059 xoá hẳn hàng khi không có giá trị,
        // chính để mọi phép đếm "bao nhiêu task đã điền trường này" trả lời đúng.
        var created = (await res.Content.ReadFromJsonAsync<MyRequestResponse>(TestJson.Options))!;
        var valueCount = await WithDbAsync(db => db.FieldValues
            .CountAsync(v => v.TaskId == created.TaskId));
        valueCount.ShouldBe(0);
    }

    [Fact]
    public async Task Diem_cuong_che_IsRequired_nay_KHONG_ap_cho_POST_tasks()
    {
        // 🔴 Phép kiểm NGƯỢC, và nó cố ý. Comment ở CustomFieldService giải thích vì sao
        // đường tạo task nội bộ KHÔNG được thừa hưởng guard G2: bật cờ bắt buộc cho một
        // trường sẽ biến hàng trăm task cũ thành không tạo lại được.
        //
        // Test này canh đúng ranh giới đó. Nếu một phiên sau "dọn dẹp cho nhất quán" bằng
        // cách đem phép kiểm về TaskService, nó sẽ ĐỎ — và đó là điều đáng xảy ra.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, "Hạ tầng");
        var field = await CreateFieldAsync(pm.Client, projectId, "Bắt buộc", FieldType.Text);
        var type = await CreateTypeAsync(pm.Client, projectId, "Có trường bắt buộc",
            requestable: true, fields: [new WorkItemTypeFieldRequest(field.Id, IsRequired: true)]);

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new Application.Features.Tasks.CreateTaskRequest(
                "Task nội bộ", projectId, null, null, null, Priority.Medium,
                WorkItemTypeId: type.Id));

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // ---------- G3: yêu cầu của người khác → 404, không 403 ----------

    [Fact]
    public async Task Yeu_cau_cua_nguoi_khac_tra_404_ke_ca_voi_PM_cua_chinh_project_do()
    {
        var (pm, outsider, projectId, typeId) = await SeedPortalAsync();
        var submitted = await SubmitAsync(outsider.Client, projectId, typeId);
        var created = (await submitted.Content.ReadFromJsonAsync<MyRequestResponse>(TestJson.Options))!;

        // PM đọc được task này qua đường NỘI BỘ, nhưng cổng chỉ phục vụ NGƯỜI GỬI.
        // Nếu cổng trả 200 cho PM thì nó đã lặng lẽ thành một đường đọc thứ hai vào project,
        // và vị từ ReporterId == me không còn là ranh giới quyền nữa.
        var viaPortal = await pm.Client.GetAsync($"/api/v1/request-portal/requests/{created.TaskId}");
        viaPortal.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var other = await CreateUserAsync();
        var viaStranger = await other.Client.GetAsync($"/api/v1/request-portal/requests/{created.TaskId}");
        viaStranger.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Danh_sach_yeu_cau_chi_hien_yeu_cau_cua_chinh_minh()
    {
        var (_, outsider, projectId, typeId) = await SeedPortalAsync();
        var someoneElse = await CreateUserAsync();

        await SubmitAsync(outsider.Client, projectId, typeId, "Của tôi");
        await SubmitAsync(someoneElse.Client, projectId, typeId, "Của người khác");

        var mine = (await outsider.Client.GetFromJsonAsync<PagedResult<MyRequestResponse>>(
            "/api/v1/request-portal/requests", TestJson.Options))!;

        mine.TotalCount.ShouldBe(1);
        mine.Items.Single().Name.ShouldBe("Của tôi");
    }

    // ---------- G4: DTO không rò rỉ nội bộ project ----------

    [Fact]
    public async Task Danh_muc_cong_KHONG_lo_noi_bo_project()
    {
        var (pm, outsider, projectId, _) = await SeedPortalAsync();

        // Dựng đúng những thứ KHÔNG được lộ, để test có cái để bắt.
        await CreateTypeAsync(pm.Client, projectId, "Việc nội bộ tuyệt mật", requestable: false);
        await CreateFieldAsync(pm.Client, projectId, "Ghi chú nội bộ", FieldType.Text);
        await CreateTaskAsync(pm.Client, projectId, "Task nội bộ tuyệt mật");

        // 🔴 Khẳng định trên JSON THÔ, không deserialize. Deserialize vào record sẽ âm thầm
        // bỏ qua mọi trường thừa và test vẫn xanh — đúng bẫy đã ghi ở ADR-048.
        var raw = await outsider.Client.GetStringAsync("/api/v1/request-portal/projects");

        raw.ShouldContain("Yêu cầu cấp quyền");            // loại MỞ cổng thì phải có
        raw.ShouldNotContain("Việc nội bộ tuyệt mật");     // loại đóng thì không
        raw.ShouldNotContain("Task nội bộ tuyệt mật");     // task của đội thì không
        raw.ShouldNotContain("Ghi chú nội bộ");            // trường không thuộc loại mở cổng
        raw.ShouldNotContain(pm.Email);                    // thành viên thì không
        raw.ShouldNotContain("boardColumn");               // cột board thì không
        raw.ShouldNotContain("member");
    }

    [Fact]
    public async Task Form_chi_lo_truong_cua_loai_MO_CONG()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, "Hạ tầng");

        var shown  = await CreateFieldAsync(pm.Client, projectId, "Hệ thống ảnh hưởng", FieldType.Text);
        var hidden = await CreateFieldAsync(pm.Client, projectId, "Ghi chú nội bộ", FieldType.Text);

        await CreateTypeAsync(pm.Client, projectId, "Yêu cầu", requestable: true,
            fields: [new WorkItemTypeFieldRequest(shown.Id, IsRequired: true)]);
        await CreateTypeAsync(pm.Client, projectId, "Nội bộ", requestable: false,
            fields: [new WorkItemTypeFieldRequest(hidden.Id, IsRequired: false)]);

        var raw = await outsider.Client.GetStringAsync(
            $"/api/v1/request-portal/projects/{projectId}/form");

        raw.ShouldContain("Hệ thống ảnh hưởng");
        raw.ShouldNotContain("Ghi chú nội bộ");
    }

    // ---------- Nghiệm thu đường (b′): cổng KHÔNG mở thêm cửa nào ----------

    [Fact]
    public async Task Cong_yeu_cau_KHONG_mo_them_cua_nao_khac()
    {
        // 🔴 ĐÂY LÀ TEST QUAN TRỌNG NHẤT CỦA ADR-063.
        //
        // Nó khẳng định chính xác điều mà đường (a) — thêm RoleInProject.Requester — sẽ phá
        // vỡ: `ProjectPermissions.IsAllowed` trả `ProjectAction.View => true` cho MỌI vai
        // trò, nên một vai trò mới sẽ mở toàn bộ danh sách dưới đây mà không một dòng code
        // nào phải đổi, và không một test nào hiện có bắt được.
        //
        // Người gửi đã gửi thành công một yêu cầu vào project — tức họ CÓ quan hệ thật với
        // nó — mà vẫn không đọc được gì. Đó là toàn bộ ý nghĩa của "cổng HẸP".
        var (_, outsider, projectId, typeId) = await SeedPortalAsync();
        (await SubmitAsync(outsider.Client, projectId, typeId)).StatusCode
            .ShouldBe(HttpStatusCode.Created);

        string[] doors =
        [
            $"/api/v1/projects/{projectId}",
            $"/api/v1/projects/{projectId}/board",
            $"/api/v1/projects/{projectId}/backlog",
            $"/api/v1/projects/{projectId}/members",
            $"/api/v1/projects/{projectId}/work-item-types",
            $"/api/v1/projects/{projectId}/fields",
            $"/api/v1/projects/{projectId}/views",
            $"/api/v1/projects/{projectId}/approval-policies",
            $"/api/v1/projects/{projectId}/activity",
            $"/api/v1/projects/{projectId}/statistics",
            $"/api/v1/projects/{projectId}/sprints",
        ];

        foreach (var door in doors)
        {
            var res = await outsider.Client.GetAsync(door);
            res.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"Cổng yêu cầu đã mở nhầm {door}");
        }
    }

    // ---------- G5: hình dạng của task sinh ra ----------

    [Fact]
    public async Task Yeu_cau_sinh_ra_task_dung_hinh_dang()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, "Hạ tầng");
        var field = await CreateFieldAsync(pm.Client, projectId, "Hệ thống", FieldType.Text);
        var type = await CreateTypeAsync(pm.Client, projectId, "Yêu cầu cấp quyền",
            requestable: true, instructions: "Nêu rõ hệ thống.",
            fields: [new WorkItemTypeFieldRequest(field.Id, IsRequired: true)]);

        var res = await SubmitAsync(outsider.Client, projectId, type.Id, "Xin quyền đọc log",
            [new SetFieldValueRequest(field.Id, ValueText: "Core Banking")]);
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await res.Content.ReadFromJsonAsync<MyRequestResponse>(TestJson.Options))!;

        var task = await WithDbAsync(db => db.Tasks
            .AsNoTracking()
            .Include(t => t.BoardColumn)
            .Include(t => t.Assignments)
            .SingleAsync(t => t.Id == created.TaskId));

        // Người gửi là Reporter — đây vừa là dữ liệu vừa là ranh giới quyền.
        task.ReporterId.ShouldBe(outsider.EmployeeId);
        // Cột trái nhất: người gửi không chọn cột, đó là ngôn ngữ của đội xử lý.
        task.BoardColumn.Order.ShouldBe(0);
        // Chưa gán ai: phân công là việc của PM.
        task.Assignments.ShouldBeEmpty();
        task.WorkItemTypeId.ShouldBe(type.Id);

        // Giá trị trường đi qua đúng đường của ADR-059 (đọc lại bằng API NỘI BỘ của PM,
        // tức chứng minh cổng ghi ra cùng một hình dạng dữ liệu chứ không phải một biến thể).
        var values = (await pm.Client.GetFromJsonAsync<List<FieldValueResponse>>(
            $"/api/v1/tasks/{created.TaskId}/field-values", TestJson.Options))!;
        values.Single(v => v.FieldDefinitionId == field.Id).ValueText.ShouldBe("Core Banking");
    }

    [Fact]
    public async Task Gui_yeu_cau_bao_cho_PM_va_ghi_nhat_ky()
    {
        var (pm, outsider, projectId, typeId) = await SeedPortalAsync();

        var before = await CountNotificationsAsync(pm.EmployeeId);
        var res = await SubmitAsync(outsider.Client, projectId, typeId);
        var created = (await res.Content.ReadFromJsonAsync<MyRequestResponse>(TestJson.Options))!;

        (await CountNotificationsAsync(pm.EmployeeId)).ShouldBe(before + 1);

        var notification = await WithDbAsync(db => db.Notifications
            .AsNoTracking()
            .Where(n => n.EmployeeId == pm.EmployeeId)
            .OrderByDescending(n => n.CreatedAt)
            .FirstAsync());

        notification.Type.ShouldBe(NotificationType.RequestSubmitted);
        // 🔴 Kind SUY RA từ Type (ADR-025). Quên thêm giá trị mới vào nhánh `Task` của
        // switch là chuông điều hướng tới /tasks/{id} với một id không phải task — đúng
        // bẫy ProjectStatusChanged đã nổ ở ADR-048.
        notification.RelatedEntityKind.ShouldBe(RelatedEntityKind.Task);
        notification.RelatedEntityId.ShouldBe(created.TaskId);

        var logged = await WithDbAsync(db => db.ActivityLogs
            .AsNoTracking()
            .AnyAsync(l => l.EntityId == created.TaskId
                        && l.Action == ActivityAction.RequestSubmitted));
        logged.ShouldBeTrue();
    }

    [Fact]
    public async Task Nguoi_gui_dong_thoi_la_PM_thi_khong_tu_bao_cho_minh()
    {
        // Không có gì cấm một PM dùng cổng của chính đội mình. Tự báo cho mình là nhiễu.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, "Hạ tầng");
        var type = await CreateTypeAsync(pm.Client, projectId, "Yêu cầu", requestable: true);

        var before = await CountNotificationsAsync(pm.EmployeeId);
        (await SubmitAsync(pm.Client, projectId, type.Id)).StatusCode
            .ShouldBe(HttpStatusCode.Created);

        (await CountNotificationsAsync(pm.EmployeeId)).ShouldBe(before);
    }

    // ---------- danh mục cổng ----------

    [Fact]
    public async Task Danh_muc_cong_chi_liet_ke_project_da_MO_cong()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();

        var open = await CreateProjectAsync(pm.Client, $"Có cổng {Guid.NewGuid():N}");
        var closedName = $"Không cổng {Guid.NewGuid():N}";
        await CreateProjectAsync(pm.Client, closedName);
        await CreateTypeAsync(pm.Client, open, "Yêu cầu", requestable: true);

        var portals = (await outsider.Client.GetFromJsonAsync<List<RequestPortalProjectResponse>>(
            "/api/v1/request-portal/projects", TestJson.Options))!;

        portals.ShouldContain(p => p.ProjectId == open);
        portals.ShouldNotContain(p => p.ProjectName == closedName);
    }

    [Fact]
    public async Task Tat_cong_thi_project_bien_khoi_danh_muc()
    {
        // Nghiệm thu chiều NGƯỢC của cờ IsRequestable: nó bật được thì cũng phải tắt được,
        // và tắt phải có hiệu lực ngay. Một cờ chỉ đi một chiều là một cái bẫy cấu hình.
        var (pm, outsider, projectId, typeId) = await SeedPortalAsync();

        var off = await pm.Client.PutAsJsonAsync($"/api/v1/work-item-types/{typeId}",
            new UpdateWorkItemTypeRequest("Yêu cầu cấp quyền", "KeyRound", "#7C3AED",
                Fields: null, IsRequestable: false));
        off.StatusCode.ShouldBe(HttpStatusCode.OK);

        var portals = (await outsider.Client.GetFromJsonAsync<List<RequestPortalProjectResponse>>(
            "/api/v1/request-portal/projects", TestJson.Options))!;
        portals.ShouldNotContain(p => p.ProjectId == projectId);

        (await SubmitAsync(outsider.Client, projectId, typeId)).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Chua_dang_nhap_thi_khong_vao_duoc_cong()
    {
        // Cổng mở cho mọi NGƯỜI ĐÃ ĐĂNG NHẬP, không phải cho ẩn danh.
        var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/request-portal/projects")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/v1/request-portal/requests")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }
}
