using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.CustomFields;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.CustomFields;

/// <summary>
/// Trường tuỳ biến theo project (ADR-059) — hậu bản của ADR-052.
///
/// <para>
/// Đây là tính năng đầu tiên cho phép người dùng đổi <b>lược đồ</b> dữ liệu của chính họ,
/// nên rủi ro nằm ở những chỗ khác hẳn CRUD thường: giá trị lạc sang project khác, lựa chọn
/// của trường này gán được vào trường kia, và cột giá trị sai kiểu.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CustomFieldsTests : IntegrationTestBase
{
    public CustomFieldsTests(PmsWebApplicationFactory factory) : base(factory) { }

    private static CreateFieldDefinitionRequest Select(string label, params string[] options)
        => new(label, FieldType.SingleSelect,
               options.Select(o => new FieldOptionRequest(o, "#3B82F6")).ToList());

    private static async Task<FieldDefinitionResponse> CreateFieldAsync(
        HttpClient client, Guid projectId, CreateFieldDefinitionRequest request)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/fields", request);
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<FieldDefinitionResponse>(TestJson.Options))!;
    }

    // ---------- lược đồ ----------

    [Fact]
    public async Task Tao_truong_Text_va_doc_lai_duoc()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var field = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Hệ thống ảnh hưởng", FieldType.Text));

        field.Label.ShouldBe("Hệ thống ảnh hưởng");
        field.Type.ShouldBe(FieldType.Text);
        field.Options.ShouldBeEmpty();
        field.ValueCount.ShouldBe(0);

        var list = await pm.Client.GetFromJsonAsync<List<FieldDefinitionResponse>>(
            $"/api/v1/projects/{projectId}/fields", TestJson.Options);
        list!.ShouldHaveSingleItem().Id.ShouldBe(field.Id);
    }

    [Fact]
    public async Task Trung_ten_truong_trong_cung_project_thi_409()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Mức rủi ro", FieldType.Text));

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/fields",
            new CreateFieldDefinitionRequest("mức rủi ro", FieldType.Text));   // khác hoa/thường

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Truong_Select_khong_co_lua_chon_nao_thi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/fields",
            new CreateFieldDefinitionRequest("Trạng thái CR", FieldType.SingleSelect));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Member_KHONG_doi_duoc_luoc_do_nhung_PM_thi_duoc()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        // Đã là thành viên nhưng vai trò không đủ -> 403, KHÔNG phải 404 (ADR-006).
        (await member.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/fields",
            new CreateFieldDefinitionRequest("Ghi chú", FieldType.Text)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // …nhưng ĐỌC được lược đồ, nếu không thì khối trường tuỳ biến trên task của họ rỗng
        // mà không có lý do nào giải thích được.
        (await member.Client.GetAsync($"/api/v1/projects/{projectId}/fields"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_chu_khong_phai_403()
    {
        var pm = await CreateUserAsync();
        var nguoiLa = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        (await nguoiLa.Client.GetAsync($"/api/v1/projects/{projectId}/fields"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Doi_ten_lua_chon_giu_nguyen_mau_va_thu_tu_cua_lua_chon_khac()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId,
            Select("Mức rủi ro", "Thấp", "Trung bình", "Cao"));

        var res = await pm.Client.PutAsJsonAsync($"/api/v1/fields/{field.Id}",
            new UpdateFieldDefinitionRequest("Mức rủi ro",
            [
                new FieldOptionRequest("Thấp", "#22C55E"),
                new FieldOptionRequest("Trung bình", "#3B82F6"),
                new FieldOptionRequest("Nghiêm trọng", "#EF4444"),   // đổi tên "Cao"
            ]));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await res.Content.ReadFromJsonAsync<FieldDefinitionResponse>(TestJson.Options))!;

        updated.Options.Count.ShouldBe(3);
        // Khớp theo Label nên "Thấp" giữ nguyên Id — giá trị các task đang trỏ tới nó không mất.
        updated.Options[0].Id.ShouldBe(field.Options[0].Id);
        updated.Options[0].Color.ShouldBe("#22C55E");   // đổi màu vẫn nhận
        // "Cao" -> "Nghiêm trọng" là một lựa chọn KHÁC về mặt dữ liệu, nên Id mới.
        updated.Options[2].Id.ShouldNotBe(field.Options[2].Id);
    }

    [Fact]
    public async Task Doi_thu_tu_thieu_mot_truong_thi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var a = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("A", FieldType.Text));
        await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("B", FieldType.Text));

        // Danh sách MỘT PHẦN bị từ chối: nhận nó thì phải định nghĩa "phần còn lại đi đâu",
        // và mọi câu trả lời đều là một luật ngầm client phải đoán.
        var res = await pm.Client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/fields/order",
            new ReorderFieldDefinitionsRequest([a.Id]));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------- giá trị ----------

    [Fact]
    public async Task Ghi_va_doc_lai_ba_kieu_gia_tri_khac_nhau()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var text = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Hệ thống", FieldType.Text));
        var number = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Giờ downtime", FieldType.Number));
        var check = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Cần rollback", FieldType.Checkbox));

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest(
            [
                new SetFieldValueRequest(text.Id, ValueText: "Core Banking"),
                new SetFieldValueRequest(number.Id, ValueNumber: 2.5m),
                new SetFieldValueRequest(check.Id, ValueBoolean: true),
            ]));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var values = (await res.Content.ReadFromJsonAsync<List<FieldValueResponse>>(TestJson.Options))!;

        values.Single(v => v.FieldDefinitionId == text.Id).ValueText.ShouldBe("Core Banking");
        // 2.5 chứ không phải 2.50 làm tròn: cột khai decimal(18,4) chứ không nhận mặc định
        // (18,2) của EF — mặc định đó làm tròn IM LẶNG.
        values.Single(v => v.FieldDefinitionId == number.Id).ValueNumber.ShouldBe(2.5m);
        values.Single(v => v.FieldDefinitionId == check.Id).ValueBoolean.ShouldBe(true);
    }

    [Fact]
    public async Task GET_tra_ve_MOI_truong_ke_ca_truong_chua_dien()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Chưa điền", FieldType.Text));

        var values = await pm.Client.GetFromJsonAsync<List<FieldValueResponse>>(
            $"/api/v1/tasks/{taskId}/field-values", TestJson.Options);

        // Frontend dựng thẳng form từ phản hồi này; nếu chỉ trả hàng đã có giá trị thì nó
        // phải tự gộp hai danh sách — đúng lớp lỗi ADR-034 đã đặt tên.
        var only = values!.ShouldHaveSingleItem();
        only.Label.ShouldBe("Chưa điền");
        only.ValueText.ShouldBeNull();
    }

    [Fact]
    public async Task Gui_null_la_XOA_gia_tri_chu_khong_phai_giu_hang_rong()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var field = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Ghi chú", FieldType.Text));

        await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id, ValueText: "x")]));
        (await WithDbAsync(db => db.FieldValues.CountAsync(v => v.TaskId == taskId))).ShouldBe(1);

        await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id)]));

        // Hàng bị XOÁ hẳn, không phải một hàng toàn null — hàng rỗng làm mọi phép đếm
        // "bao nhiêu task đã điền trường này" trả lời sai.
        (await WithDbAsync(db => db.FieldValues.CountAsync(v => v.TaskId == taskId))).ShouldBe(0);
    }

    [Fact]
    public async Task PATCH_chi_dung_toi_truong_duoc_nhac_den()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var a = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("A", FieldType.Text));
        var b = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("B", FieldType.Text));

        await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest(
            [
                new SetFieldValueRequest(a.Id, ValueText: "giữ nguyên"),
                new SetFieldValueRequest(b.Id, ValueText: "cũng vậy"),
            ]));

        // Chỉ nhắc tới B -> A phải còn nguyên. Nếu đây là PUT thì A đã bị xoá.
        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(b.Id, ValueText: "đổi rồi")]));

        var values = (await res.Content.ReadFromJsonAsync<List<FieldValueResponse>>(TestJson.Options))!;
        values.Single(v => v.FieldDefinitionId == a.Id).ValueText.ShouldBe("giữ nguyên");
        values.Single(v => v.FieldDefinitionId == b.Id).ValueText.ShouldBe("đổi rồi");
    }

    [Fact]
    public async Task Chon_MultiSelect_nhieu_gia_tri_va_bo_bot_deu_dung()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res0 = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/fields",
            new CreateFieldDefinitionRequest("Hệ thống ảnh hưởng", FieldType.MultiSelect,
            [
                new FieldOptionRequest("Core Banking", "#EF4444"),
                new FieldOptionRequest("Internet Banking", "#3B82F6"),
                new FieldOptionRequest("ATM", "#22C55E"),
            ]));
        var field = (await res0.Content.ReadFromJsonAsync<FieldDefinitionResponse>(TestJson.Options))!;

        var first = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id,
                SelectedOptionIds: [field.Options[0].Id, field.Options[2].Id])]));

        // Khẳng định luôn lần ghi ĐẦU: thiếu dòng này thì một lỗi 500 ở đây sẽ hiện ra dưới
        // dạng "JSON không parse được" ở lần ghi thứ hai — sai chỗ, sai cả nguyên nhân.
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());

        // Bỏ bớt còn MỘT: đây là chỗ many-to-many dễ sai nhất — EF chỉ sinh DELETE cho bảng
        // nối khi nó theo dõi được trạng thái TRƯỚC của tập hợp (xem ListValuesForTaskAsync).
        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id,
                SelectedOptionIds: [field.Options[2].Id])]));

        res.StatusCode.ShouldBe(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        var values = (await res.Content.ReadFromJsonAsync<List<FieldValueResponse>>(TestJson.Options))!;
        var selected = values.Single(v => v.FieldDefinitionId == field.Id).SelectedOptions;

        selected.ShouldHaveSingleItem().Label.ShouldBe("ATM");
    }

    [Fact]
    public async Task SingleSelect_chon_hai_gia_tri_thi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var field = await CreateFieldAsync(pm.Client, projectId,
            Select("Mức rủi ro", "Thấp", "Cao"));

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id,
                SelectedOptionIds: [field.Options[0].Id, field.Options[1].Id])]));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// 🔴 Phép kiểm đắt nhất file này. Bảng nối <c>FieldValueOptions</c> không biết gì về
    /// quan hệ "option thuộc trường nào", nên không có <c>ResolveOptionIds</c> thì một
    /// option của trường KHÁC — kể cả của PROJECT khác — vẫn ghi xuống được, và giao diện
    /// sẽ hiện một chip không có trong danh sách của chính trường đang xem.
    /// </summary>
    [Fact]
    public async Task Gan_lua_chon_cua_TRUONG_KHAC_thi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var risk = await CreateFieldAsync(pm.Client, projectId, Select("Mức rủi ro", "Thấp", "Cao"));
        var env = await CreateFieldAsync(pm.Client, projectId, Select("Môi trường", "UAT", "PROD"));

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(risk.Id,
                SelectedOptionIds: [env.Options[0].Id])]));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.FieldValues.CountAsync(v => v.TaskId == taskId))).ShouldBe(0);
    }

    /// <summary>
    /// Trường của project KHÁC không được ghi vào task này. Không chặn thì giá trị lạc sang
    /// project khác và không màn nào hiển thị nó — dữ liệu "mất tích" mà vẫn nằm trong DB.
    /// </summary>
    [Fact]
    public async Task Gan_truong_cua_PROJECT_KHAC_thi_404()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "A");
        var projectB = await CreateProjectAsync(pm.Client, "B");
        var taskInA = await CreateTaskAsync(pm.Client, projectA);

        var fieldInB = await CreateFieldAsync(pm.Client, projectB,
            new CreateFieldDefinitionRequest("Của B", FieldType.Text));

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskInA}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(fieldInB.Id, ValueText: "x")]));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_doc_duoc_gia_tri_nhung_khong_ghi_duoc()
    {
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var field = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Ghi chú", FieldType.Text));

        (await viewer.Client.GetAsync($"/api/v1/tasks/{taskId}/field-values"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await viewer.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id, ValueText: "x")])))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Xoa_truong_thi_moi_gia_tri_cua_no_bien_mat_theo()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var field = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Sắp xoá", FieldType.Text));

        await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id, ValueText: "x")]));

        // ValueCount nuôi cảnh báo "trường này đang có N giá trị" trước khi xoá.
        var before = await pm.Client.GetFromJsonAsync<List<FieldDefinitionResponse>>(
            $"/api/v1/projects/{projectId}/fields", TestJson.Options);
        before!.ShouldHaveSingleItem().ValueCount.ShouldBe(1);

        (await pm.Client.DeleteAsync($"/api/v1/fields/{field.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Cascade ở tầng DB (FieldValueConfiguration) — không có dòng rác nào ở lại.
        (await WithDbAsync(db => db.FieldValues.CountAsync(v => v.FieldDefinitionId == field.Id)))
            .ShouldBe(0);
    }

    /// <summary>
    /// Kiểu Date phải về nửa đêm UTC. Giữ phần giờ do client gửi sẽ làm hai người ở hai múi
    /// giờ thấy hai NGÀY khác nhau cho cùng một giá trị — đúng lớp lỗi ADR-046b đã xử lý.
    /// </summary>
    [Fact]
    public async Task Kieu_Date_cat_bo_phan_gio()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var field = await CreateFieldAsync(pm.Client, projectId,
            new CreateFieldDefinitionRequest("Cửa sổ bảo trì", FieldType.Date));

        await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(field.Id,
                ValueDate: new DateTime(2026, 8, 20, 23, 45, 12, DateTimeKind.Utc))]));

        var stored = await WithDbAsync(db => db.FieldValues
            .Where(v => v.TaskId == taskId).Select(v => v.ValueDate).SingleAsync());

        stored.ShouldBe(new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc));
    }
}
