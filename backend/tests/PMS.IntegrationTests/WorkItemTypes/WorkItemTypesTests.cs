using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.CustomFields;
using PMS.Application.Features.Tasks;
using PMS.Application.Features.WorkItemTypes;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.WorkItemTypes;

/// <summary>
/// Loại công việc theo project (ADR-060) — xây trên nền trường tuỳ biến của ADR-059.
///
/// <para>
/// Rủi ro của cụm này nằm ở ba chỗ, và test bám đúng ba chỗ đó: bất biến "mọi task luôn
/// thuộc một loại tồn tại", ranh giới chéo project, và <c>IsRequired</c> — thứ ADR-059 cố ý
/// hoãn vì lúc đó chưa có điểm cưỡng chế nào.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class WorkItemTypesTests : IntegrationTestBase
{
    public WorkItemTypesTests(PmsWebApplicationFactory factory) : base(factory) { }

    private static async Task<List<WorkItemTypeResponse>> ListTypesAsync(
        HttpClient client, Guid projectId)
        => (await client.GetFromJsonAsync<List<WorkItemTypeResponse>>(
            $"/api/v1/projects/{projectId}/work-item-types", TestJson.Options))!;

    private static async Task<WorkItemTypeResponse> CreateTypeAsync(
        HttpClient client, Guid projectId, string name,
        IReadOnlyList<WorkItemTypeFieldRequest>? fields = null)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/work-item-types",
            new CreateWorkItemTypeRequest(name, "CircleAlert", "#EF4444", fields));
        res.StatusCode.ShouldBe(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<WorkItemTypeResponse>(TestJson.Options))!;
    }

    private static async Task<FieldDefinitionResponse> CreateFieldAsync(
        HttpClient client, Guid projectId, string label)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/fields",
            new CreateFieldDefinitionRequest(label, FieldType.Text));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<FieldDefinitionResponse>(TestJson.Options))!;
    }

    // ---------- bất biến "luôn có một loại" ----------

    [Fact]
    public async Task Project_moi_duoc_cap_san_loai_mac_dinh_Task()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var types = await ListTypesAsync(pm.Client, projectId);

        // Project không có loại nào = project không tạo được task nào. Bất biến này được
        // giữ ngay lúc tạo project, không phải kiểm lúc tạo task đầu tiên.
        var only = types.ShouldHaveSingleItem();
        only.Name.ShouldBe("Task");
        only.Order.ShouldBe(0);
    }

    [Fact]
    public async Task Task_moi_khong_chi_dinh_loai_thi_vao_loai_mac_dinh()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);

        detail!.Type.Name.ShouldBe("Task");
    }

    [Fact]
    public async Task Tao_task_voi_loai_chi_dinh_va_doi_loai_qua_PUT()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var incident = await CreateTypeAsync(pm.Client, projectId, "Sự cố");

        var created = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Mất kết nối Core", projectId, null, null, null,
                Priority.High, WorkItemTypeId: incident.Id));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var summary = (await created.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options))!;

        summary.Type.TypeId.ShouldBe(incident.Id);
        summary.Type.Name.ShouldBe("Sự cố");
        summary.Type.Color.ShouldBe("#EF4444");

        // Đổi sang loại mặc định qua PUT
        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{summary.Id}", TestJson.Options);
        var defaultType = (await ListTypesAsync(pm.Client, projectId)).Single(t => t.Name == "Task");

        var updated = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{summary.Id}",
            new UpdateTaskRequest("Mất kết nối Core", null, Priority.High, detail!.RowVersion,
                WorkItemTypeId: defaultType.Id));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<TaskDetailResponse>(TestJson.Options))!
            .Type.TypeId.ShouldBe(defaultType.Id);
    }

    /// <summary>
    /// 🔴 <c>null</c> = GIỮ NGUYÊN, không phải "về mặc định". Client cũ không gửi trường
    /// này, và biến sự vắng mặt thành lệnh reset sẽ âm thầm đổi loại của mọi task được sửa
    /// bởi một bản frontend chưa cập nhật.
    /// </summary>
    [Fact]
    public async Task PUT_khong_gui_loai_thi_GIU_NGUYEN_loai_hien_tai()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var incident = await CreateTypeAsync(pm.Client, projectId, "Sự cố");

        var created = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("T", projectId, null, null, null,
                Priority.Medium, WorkItemTypeId: incident.Id));
        var summary = (await created.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options))!;
        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{summary.Id}", TestJson.Options);

        var updated = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{summary.Id}",
            new UpdateTaskRequest("T đổi tên", null, Priority.Medium, detail!.RowVersion));

        (await updated.Content.ReadFromJsonAsync<TaskDetailResponse>(TestJson.Options))!
            .Type.TypeId.ShouldBe(incident.Id);
    }

    [Fact]
    public async Task Khong_xoa_duoc_loai_cuoi_cung()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var only = (await ListTypesAsync(pm.Client, projectId)).Single();

        var res = await pm.Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/work-item-types/{only.Id}")
        {
            Content = JsonContent.Create(new DeleteWorkItemTypeRequest()),
        });

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Xoa_loai_con_task_ma_khong_chon_dich_thi_400_kem_so_task()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var incident = await CreateTypeAsync(pm.Client, projectId, "Sự cố");

        await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("T", projectId, null, null, null,
                Priority.Medium, WorkItemTypeId: incident.Id));

        var res = await pm.Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/work-item-types/{incident.Id}")
        {
            Content = JsonContent.Create(new DeleteWorkItemTypeRequest()),
        });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // Số task phải có trong thông điệp: người dùng cần biết quy mô việc mình sắp làm,
        // không chỉ biết là "phải chọn thêm gì đó".
        (await res.Content.ReadAsStringAsync()).ShouldContain("1 task");
    }

    [Fact]
    public async Task Xoa_loai_co_dich_thi_chuyen_het_task_sang_loai_do()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var incident = await CreateTypeAsync(pm.Client, projectId, "Sự cố");
        var defaultType = (await ListTypesAsync(pm.Client, projectId)).Single(t => t.Name == "Task");

        var created = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("T", projectId, null, null, null,
                Priority.Medium, WorkItemTypeId: incident.Id));
        var summary = (await created.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options))!;

        var res = await pm.Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/work-item-types/{incident.Id}")
        {
            Content = JsonContent.Create(new DeleteWorkItemTypeRequest(defaultType.Id)),
        });
        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var typeId = await WithDbAsync(db => db.Tasks
            .Where(t => t.Id == summary.Id).Select(t => t.WorkItemTypeId).SingleAsync());

        typeId.ShouldBe(defaultType.Id);
    }

    // ---------- ranh giới chéo project ----------

    [Fact]
    public async Task Tao_task_voi_loai_cua_PROJECT_KHAC_thi_404()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "A");
        var projectB = await CreateProjectAsync(pm.Client, "B");
        var typeInB = await CreateTypeAsync(pm.Client, projectB, "Của B");

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("T", projectA, null, null, null,
                Priority.Medium, WorkItemTypeId: typeInB.Id));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Gắn trường của project khác vào loại này sẽ làm
    /// <c>GET /tasks/{id}/field-values</c> trả về một trường mà chính project không có —
    /// một ô nhập không thuộc về đâu cả.
    /// </summary>
    [Fact]
    public async Task Gan_truong_cua_PROJECT_KHAC_vao_loai_thi_404()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "A");
        var projectB = await CreateProjectAsync(pm.Client, "B");
        var fieldInB = await CreateFieldAsync(pm.Client, projectB, "Của B");

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectA}/work-item-types",
            new CreateWorkItemTypeRequest("Sự cố", "CircleAlert", "#EF4444",
                [new WorkItemTypeFieldRequest(fieldInB.Id, false)]));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Member_khong_doi_duoc_loai_nhung_doc_duoc()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        (await member.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/work-item-types",
            new CreateWorkItemTypeRequest("Sự cố", "CircleAlert", "#EF4444")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await member.Client.GetAsync($"/api/v1/projects/{projectId}/work-item-types"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Trung_ten_loai_trong_cung_project_thi_409()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await CreateTypeAsync(pm.Client, projectId, "Sự cố");

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/work-item-types",
            new CreateWorkItemTypeRequest("sự cố", "CircleAlert", "#EF4444"));

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---------- trường theo loại + IsRequired ----------

    [Fact]
    public async Task Truong_moi_tu_gan_vao_MOI_loai_nen_hanh_vi_ADR059_khong_doi()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await CreateTypeAsync(pm.Client, projectId, "Sự cố");

        var field = await CreateFieldAsync(pm.Client, projectId, "Hệ thống");

        // Trường vừa tạo phải hiện ở CẢ hai loại. Không có luật này thì tạo trường xong nó
        // vô hình ở mọi task cho tới khi người dùng đoán ra là còn phải đi gắn vào từng loại.
        var types = await ListTypesAsync(pm.Client, projectId);
        types.Count.ShouldBe(2);
        types.ShouldAllBe(t => t.Fields.Any(f => f.FieldDefinitionId == field.Id));
        types.ShouldAllBe(t => t.Fields.All(f => !f.IsRequired));
    }

    [Fact]
    public async Task Truong_khong_thuoc_loai_cua_task_thi_KHONG_hien_o_field_values()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId, "Chỉ cho Sự cố");
        var defaultType = (await ListTypesAsync(pm.Client, projectId)).Single(t => t.Name == "Task");

        // Gỡ trường khỏi loại mặc định
        (await pm.Client.PutAsJsonAsync($"/api/v1/work-item-types/{defaultType.Id}",
            new UpdateWorkItemTypeRequest("Task", "CircleDot", "#6B7280", [])))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var values = await pm.Client.GetFromJsonAsync<List<FieldValueResponse>>(
            $"/api/v1/tasks/{taskId}/field-values", TestJson.Options);

        values.ShouldBeEmpty();
        _ = field;
    }

    /// <summary>
    /// 🔴 Trường ĐÃ CÓ GIÁ TRỊ vẫn phải hiện ra dù loại không (còn) khai nó.
    ///
    /// <para>
    /// Không có luật này thì gỡ một trường khỏi loại làm giá trị đã nhập biến mất khỏi giao
    /// diện trong khi vẫn nằm trong DB — đúng lớp "dữ liệu mất tích" mà ADR-059 đã chặn ở
    /// chiều project. Người dùng phải nhìn thấy để còn xoá đi.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Truong_da_co_gia_tri_van_hien_du_bi_go_khoi_loai()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId, "Hệ thống");
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var defaultType = (await ListTypesAsync(pm.Client, projectId)).Single();

        await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id, ValueText: "Core")]));

        // Gỡ trường khỏi loại SAU khi task đã có giá trị
        await pm.Client.PutAsJsonAsync($"/api/v1/work-item-types/{defaultType.Id}",
            new UpdateWorkItemTypeRequest("Task", "CircleDot", "#6B7280", []));

        var values = await pm.Client.GetFromJsonAsync<List<FieldValueResponse>>(
            $"/api/v1/tasks/{taskId}/field-values", TestJson.Options);

        var only = values!.ShouldHaveSingleItem();
        only.FieldDefinitionId.ShouldBe(field.Id);
        only.ValueText.ShouldBe("Core");
        only.IsRequired.ShouldBeFalse();   // không còn thuộc loại -> không thể bắt buộc
    }

    [Fact]
    public async Task Truong_bat_buoc_thi_KHONG_xoa_duoc_gia_tri()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId, "Hệ thống ảnh hưởng");
        var defaultType = (await ListTypesAsync(pm.Client, projectId)).Single();

        (await pm.Client.PutAsJsonAsync($"/api/v1/work-item-types/{defaultType.Id}",
            new UpdateWorkItemTypeRequest("Task", "CircleDot", "#6B7280",
                [new WorkItemTypeFieldRequest(field.Id, IsRequired: true)])))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var taskId = await CreateTaskAsync(pm.Client, projectId);

        // Điền được bình thường
        (await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id, ValueText: "Core")])))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Nhưng xoá đi thì bị chặn — đây là ĐIỂM CƯỠNG CHẾ mà ADR-059 chưa có.
        var cleared = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id)]));

        cleared.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await cleared.Content.ReadAsStringAsync()).ShouldContain("bắt buộc");

        // Giá trị cũ còn nguyên, không bị xoá nửa chừng
        var values = await pm.Client.GetFromJsonAsync<List<FieldValueResponse>>(
            $"/api/v1/tasks/{taskId}/field-values", TestJson.Options);
        values!.Single(v => v.FieldDefinitionId == field.Id).ValueText.ShouldBe("Core");
    }

    /// <summary>
    /// Bật <c>IsRequired</c> KHÔNG áp ngược lên task đã có — task cũ để trống vẫn sửa được
    /// các trường khác bình thường. Đây chính là lý do ADR-059 hoãn cờ này thay vì đặt nó
    /// trên <c>FieldDefinition</c>: ở đó nó sẽ biến hàng trăm task cũ thành không hợp lệ.
    /// </summary>
    [Fact]
    public async Task Bat_bat_buoc_KHONG_lam_hong_task_da_co()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var required = await CreateFieldAsync(pm.Client, projectId, "Bắt buộc");
        var other = await CreateFieldAsync(pm.Client, projectId, "Tuỳ chọn");
        var taskId = await CreateTaskAsync(pm.Client, projectId);   // chưa điền gì
        var defaultType = (await ListTypesAsync(pm.Client, projectId)).Single();

        await pm.Client.PutAsJsonAsync($"/api/v1/work-item-types/{defaultType.Id}",
            new UpdateWorkItemTypeRequest("Task", "CircleDot", "#6B7280",
            [
                new WorkItemTypeFieldRequest(required.Id, IsRequired: true),
                new WorkItemTypeFieldRequest(other.Id, IsRequired: false),
            ]));

        // Task cũ đang để trống trường bắt buộc — vẫn phải ghi được trường KHÁC.
        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(other.Id, ValueText: "ok")]));

        res.StatusCode.ShouldBe(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Doi_thu_tu_thieu_mot_loai_thi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var incident = await CreateTypeAsync(pm.Client, projectId, "Sự cố");

        var res = await pm.Client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-item-types/order",
            new ReorderWorkItemTypesRequest([incident.Id]));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
