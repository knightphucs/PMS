using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.CustomFields;
using PMS.Application.Features.SavedViews;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.SavedViews;

/// <summary>
/// View lưu được (ADR-061) — mảnh cuối của nền tảng mở rộng.
///
/// <para>
/// Rủi ro của tính năng này nằm ở ba chỗ khác hẳn CRUD thường:
/// <list type="number">
/// <item><b>Ranh giới riêng/chia sẻ</b> — view riêng của người khác không được rò rỉ, kể cả
/// qua mã trạng thái (404 chứ không 403).</item>
/// <item><b>Toàn vẹn tham chiếu</b> — xoá một trường tuỳ biến phải dọn sạch mọi điều kiện
/// trỏ vào nó. Đó là toàn bộ lý do bộ lọc là BẢNG chứ không phải JSON.</item>
/// <item><b>So sánh đúng KIỂU</b> — <c>9</c> phải nhỏ hơn <c>10</c>. Đây là chỗ quyết định
/// "cột có kiểu" của ADR-059 được nghiệm thu.</item>
/// </list>
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SavedViewsTests : IntegrationTestBase
{
    public SavedViewsTests(PmsWebApplicationFactory factory) : base(factory) { }

    // ---------- helper ----------

    private static async Task<SavedViewResponse> CreateViewAsync(
        HttpClient client, Guid projectId, CreateSavedViewRequest request)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/views", request);
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<SavedViewResponse>(TestJson.Options))!;
    }

    private static CreateSavedViewRequest View(
        string name, bool shared = false, params SavedViewFilterDto[] filters)
        => new(name, shared, null, false, null, filters.ToList());

    private static async Task<FieldDefinitionResponse> CreateFieldAsync(
        HttpClient client, Guid projectId, string label, FieldType type,
        params string[] options)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/fields",
            new CreateFieldDefinitionRequest(label, type,
                options.Select(o => new FieldOptionRequest(o, "#3B82F6")).ToList()));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<FieldDefinitionResponse>(TestJson.Options))!;
    }

    private static async Task SetNumberAsync(
        HttpClient client, Guid taskId, Guid fieldId, decimal value)
    {
        var res = await client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/field-values",
            new SetFieldValuesRequest([new SetFieldValueRequest(fieldId, ValueNumber: value)]));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<PagedResult<TaskListItemResponse>> QueryAsync(
        HttpClient client, Guid projectId, TaskQueryRequest request)
    {
        var res = await client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/query", request);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<PagedResult<TaskListItemResponse>>(
            TestJson.Options))!;
    }

    // ---------- CRUD + ranh giới riêng/chia sẻ ----------

    [Fact]
    public async Task Tao_view_rieng_va_doc_lai_duoc()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var view = await CreateViewAsync(pm.Client, projectId,
            View("Việc quá hạn", false,
                 new SavedViewFilterDto(TaskField.Category, null, FilterOperator.NotEquals, "Done")));

        view.Name.ShouldBe("Việc quá hạn");
        view.IsShared.ShouldBeFalse();
        view.OwnerId.ShouldBe(pm.EmployeeId);
        view.OwnerName.ShouldNotBeNullOrWhiteSpace();   // navigation Owner chưa nạp lúc tạo
        view.CanEdit.ShouldBeTrue();
        view.Filters.ShouldHaveSingleItem().Value.ShouldBe("Done");

        var list = await pm.Client.GetFromJsonAsync<List<SavedViewResponse>>(
            $"/api/v1/projects/{projectId}/views", TestJson.Options);
        list!.ShouldHaveSingleItem().Id.ShouldBe(view.Id);
    }

    [Fact]
    public async Task View_rieng_cua_nguoi_khac_KHONG_hien_trong_danh_sach()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        await CreateViewAsync(member.Client, projectId, View("Của riêng tôi"));

        var seenByPm = await pm.Client.GetFromJsonAsync<List<SavedViewResponse>>(
            $"/api/v1/projects/{projectId}/views", TestJson.Options);

        // Kể cả PM cũng không thấy: tên một view có thể tiết lộ thứ người ta đang theo dõi.
        seenByPm!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Viewer_van_tao_duoc_view_RIENG()
    {
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        // Ngoại lệ có chủ đích, cùng lý lẽ "theo dõi task" của ADR-036: nó chỉ đổi cách
        // chính họ nhìn danh sách, không ai khác thấy.
        var view = await CreateViewAsync(viewer.Client, projectId, View("Tôi theo dõi"));
        view.OwnerId.ShouldBe(viewer.EmployeeId);
    }

    [Fact]
    public async Task Member_tao_view_CHIA_SE_thi_403()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        var res = await member.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/views",
            View("Hàng đợi của đội", shared: true));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task View_chia_se_thi_moi_thanh_vien_deu_thay()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        var shared = await CreateViewAsync(pm.Client, projectId, View("Hàng đợi", shared: true));

        var seen = await member.Client.GetFromJsonAsync<List<SavedViewResponse>>(
            $"/api/v1/projects/{projectId}/views", TestJson.Options);

        var only = seen!.ShouldHaveSingleItem();
        only.Id.ShouldBe(shared.Id);
        // Thấy được nhưng KHÔNG sửa được — backend trả lời thay vì để frontend tự suy.
        only.CanEdit.ShouldBeFalse();
    }

    [Fact]
    public async Task Sua_view_RIENG_cua_nguoi_khac_thi_404_chu_khong_403()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        var mine = await CreateViewAsync(member.Client, projectId, View("Riêng tư"));

        var res = await pm.Client.PutAsJsonAsync($"/api/v1/views/{mine.Id}",
            new UpdateSavedViewRequest("Đổi tên", false, null, false, null));

        // 403 sẽ xác nhận "view này có thật" — đúng thứ ListVisibleAsync cố ý không trả về.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PM_sua_duoc_view_CHIA_SE_cua_nguoi_khac()
    {
        var pm = await CreateUserAsync();
        var other = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, other, projectId, RoleInProject.ProjectManager);

        var shared = await CreateViewAsync(other.Client, projectId, View("Chung", shared: true));

        var res = await pm.Client.PutAsJsonAsync($"/api/v1/views/{shared.Id}",
            new UpdateSavedViewRequest("Chung (đã sửa)", true, null, false, null));

        // Không thì view chung khoá chết khi chủ sở hữu rời dự án.
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Hai_nguoi_KHAC_nhau_dat_trung_ten_view_rieng_thi_duoc()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        await CreateViewAsync(pm.Client, projectId, View("Việc của tôi"));
        // Họ không nhìn thấy view của nhau nên trùng tên không gây nhầm lẫn gì.
        await CreateViewAsync(member.Client, projectId, View("Việc của tôi"));
    }

    [Fact]
    public async Task Cung_mot_nguoi_dat_trung_ten_thi_409()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await CreateViewAsync(pm.Client, projectId, View("Việc của tôi"));

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/views",
            View("việc của tôi"));   // khác hoa/thường

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/views");
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- toàn vẹn tham chiếu ----------

    [Fact]
    public async Task Bo_loc_tro_vao_truong_cua_project_KHAC_thi_404()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "A");
        var projectB = await CreateProjectAsync(pm.Client, "B");

        var fieldOfB = await CreateFieldAsync(pm.Client, projectB, "Mức rủi ro", FieldType.Text);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectA}/views",
            View("Lạc chỗ", false,
                 new SavedViewFilterDto(null, fieldOfB.Id, FilterOperator.Equals, "Cao")));

        // Không chặn thì view lọc theo một trường không bao giờ khớp, và người dùng không
        // hiểu vì sao danh sách luôn rỗng.
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Xoa_truong_tuy_bien_thi_dieu_kien_tro_vao_no_BIEN_MAT_theo()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId, "Giờ downtime", FieldType.Number);

        var view = await CreateViewAsync(pm.Client, projectId,
            View("Downtime lớn", false,
                 new SavedViewFilterDto(null, field.Id, FilterOperator.GreaterThan, "2")));

        (await WithDbAsync(db => db.SavedViewFilters.CountAsync(f => f.SavedViewId == view.Id)))
            .ShouldBe(1);

        (await pm.Client.DeleteAsync($"/api/v1/fields/{field.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 🔑 ĐÂY là lý do bộ lọc là BẢNG chứ không phải JSON: cascade của DB dọn sạch.
        // Với JSON thì điều kiện vẫn nằm đó, trỏ vào một trường không còn tồn tại.
        (await WithDbAsync(db => db.SavedViewFilters.CountAsync(f => f.SavedViewId == view.Id)))
            .ShouldBe(0);

        // Bản thân view thì SỐNG — mất một điều kiện, không mất cả góc nhìn.
        (await WithDbAsync(db => db.SavedViews.AnyAsync(v => v.Id == view.Id))).ShouldBeTrue();
    }

    [Fact]
    public async Task Xoa_view_thi_dieu_kien_va_cot_bien_mat_theo()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var view = await CreateViewAsync(pm.Client, projectId,
            new CreateSavedViewRequest("Tạm", false, null, false, null,
                [new SavedViewFilterDto(TaskField.Priority, null, FilterOperator.Equals, "High")],
                [new SavedViewColumnDto(TaskField.Name, null)]));

        (await pm.Client.DeleteAsync($"/api/v1/views/{view.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await WithDbAsync(db => db.SavedViewFilters.CountAsync(f => f.SavedViewId == view.Id)))
            .ShouldBe(0);
        (await WithDbAsync(db => db.SavedViewColumns.CountAsync(c => c.SavedViewId == view.Id)))
            .ShouldBe(0);
    }

    // ---------- kiểm đầu vào ----------

    [Fact]
    public async Task Toan_tu_khong_hop_le_voi_kieu_thi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId, "Giờ downtime", FieldType.Number);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/views",
            View("Sai toán tử", false,
                 new SavedViewFilterDto(null, field.Id, FilterOperator.Contains, "2")));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Gia_tri_khong_parse_duoc_thi_400_ngay_luc_LUU()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId, "Giờ downtime", FieldType.Number);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/views",
            View("Số hỏng", false,
                 new SavedViewFilterDto(null, field.Id, FilterOperator.Equals, "không phải số")));

        // Fail NGAY lúc lưu. Để tới lúc chạy view mới báo thì người dùng ôm một view hỏng
        // mà không biết mình đã lưu sai cái gì.
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dieu_kien_khong_khai_nguon_truong_nao_thi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/views",
            View("Rỗng", false,
                 new SavedViewFilterDto(null, null, FilterOperator.Equals, "x")));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Gia_tri_enum_sai_ten_thi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/views",
            View("Ưu tiên lạ", false,
                 new SavedViewFilterDto(TaskField.Priority, null, FilterOperator.Equals, "Khẩn")));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------- chạy bộ lọc ----------

    [Fact]
    public async Task Loc_so_tren_truong_tuy_bien_so_DUNG_KIEU_9_nho_hon_10()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId, "Giờ downtime", FieldType.Number);

        var nine = await CreateTaskAsync(pm.Client, projectId, "Chín");
        var ten  = await CreateTaskAsync(pm.Client, projectId, "Mười");
        await SetNumberAsync(pm.Client, nine, field.Id, 9);
        await SetNumberAsync(pm.Client, ten,  field.Id, 10);

        var page = await QueryAsync(pm.Client, projectId, new TaskQueryRequest(
            [new SavedViewFilterDto(null, field.Id, FilterOperator.GreaterThan, "9")]));

        // 🔑 Nghiệm thu quyết định "cột có kiểu" của ADR-059. So chuỗi thì "10" < "9" và
        // task "Mười" sẽ KHÔNG lọt lưới — bộ lọc trả về rỗng mà không báo lỗi gì.
        page.Items.ShouldHaveSingleItem().Task.Id.ShouldBe(ten);
    }

    [Fact]
    public async Task Loc_theo_lua_chon_cua_truong_Select()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(
            pm.Client, projectId, "Mức rủi ro", FieldType.SingleSelect, "Cao", "Thấp");

        var high = await CreateTaskAsync(pm.Client, projectId, "Rủi ro cao");
        await CreateTaskAsync(pm.Client, projectId, "Chưa đánh giá");

        var cao = field.Options.Single(o => o.Label == "Cao");
        (await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{high}/field-values",
            new SetFieldValuesRequest([
                new SetFieldValueRequest(field.Id, SelectedOptionIds: [cao.Id])])))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await QueryAsync(pm.Client, projectId, new TaskQueryRequest(
            [new SavedViewFilterDto(null, field.Id, FilterOperator.Equals, cao.Id.ToString())]));

        var item = page.Items.ShouldHaveSingleItem();
        item.Task.Id.ShouldBe(high);
        // Màn danh sách hiển thị được cột trường tuỳ biến -> giá trị phải đi kèm.
        item.CustomFields.ShouldHaveSingleItem()
            .SelectedOptions.ShouldHaveSingleItem().Label.ShouldBe("Cao");
    }

    [Fact]
    public async Task Loc_truong_tuy_bien_RONG_bao_gom_task_chua_dien()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var field = await CreateFieldAsync(pm.Client, projectId, "Giờ downtime", FieldType.Number);

        var filled = await CreateTaskAsync(pm.Client, projectId, "Đã điền");
        var empty  = await CreateTaskAsync(pm.Client, projectId, "Chưa điền");
        await SetNumberAsync(pm.Client, filled, field.Id, 5);

        var page = await QueryAsync(pm.Client, projectId, new TaskQueryRequest(
            [new SavedViewFilterDto(null, field.Id, FilterOperator.IsEmpty, null)]));

        // "Rỗng" = KHÔNG CÓ HÀNG: ADR-059 xoá hẳn hàng khi người dùng xoá trắng giá trị.
        page.Items.ShouldHaveSingleItem().Task.Id.ShouldBe(empty);
    }

    [Fact]
    public async Task Loc_uu_tien_theo_TEN_enum()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var high = await CreateTaskAsync(pm.Client, projectId, "Gấp", priority: Priority.Highest);
        await CreateTaskAsync(pm.Client, projectId, "Thường", priority: Priority.Low);

        var page = await QueryAsync(pm.Client, projectId, new TaskQueryRequest(
            [new SavedViewFilterDto(TaskField.Priority, null, FilterOperator.Equals, "Highest")]));

        // Khớp theo TÊN chứ không theo số: ADR-052 đã trả giá cho việc coi số của một enum
        // là ổn định.
        page.Items.ShouldHaveSingleItem().Task.Id.ShouldBe(high);
    }

    [Fact]
    public async Task Loc_Sprint_rong_tra_ve_task_o_Backlog()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);

        var backlog = await CreateTaskAsync(pm.Client, projectId, "Ở backlog");
        await CreateTaskAsync(pm.Client, projectId, "Trong sprint", sprintId: sprintId);

        var page = await QueryAsync(pm.Client, projectId, new TaskQueryRequest(
            [new SavedViewFilterDto(TaskField.Sprint, null, FilterOperator.IsEmpty, null)]));

        page.Items.ShouldHaveSingleItem().Task.Id.ShouldBe(backlog);
    }

    [Fact]
    public async Task Loc_Assignee_rong_tra_ve_task_chua_ai_nhan()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var unassigned = await CreateTaskAsync(pm.Client, projectId, "Chưa ai nhận");
        var taken = await CreateTaskAsync(pm.Client, projectId, "Đã nhận");

        (await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taken}/assignees/me", new { }))
            .IsSuccessStatusCode.ShouldBeTrue();

        var page = await QueryAsync(pm.Client, projectId, new TaskQueryRequest(
            [new SavedViewFilterDto(TaskField.Assignee, null, FilterOperator.IsEmpty, null)]));

        page.Items.ShouldHaveSingleItem().Task.Id.ShouldBe(unassigned);
    }

    [Fact]
    public async Task Nhieu_dieu_kien_noi_bang_AND()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var match = await CreateTaskAsync(pm.Client, projectId, "Khớp cả hai",
            priority: Priority.Highest, storyPoints: 8);
        await CreateTaskAsync(pm.Client, projectId, "Chỉ khớp ưu tiên",
            priority: Priority.Highest, storyPoints: 1);

        var page = await QueryAsync(pm.Client, projectId, new TaskQueryRequest([
            new SavedViewFilterDto(TaskField.Priority, null, FilterOperator.Equals, "Highest"),
            new SavedViewFilterDto(TaskField.StoryPoints, null, FilterOperator.GreaterThanOrEqual, "5"),
        ]));

        page.Items.ShouldHaveSingleItem().Task.Id.ShouldBe(match);
    }

    [Fact]
    public async Task Truy_van_KHONG_tra_task_cua_project_khac()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "A");
        var projectB = await CreateProjectAsync(pm.Client, "B");

        var inA = await CreateTaskAsync(pm.Client, projectA, "Của A");
        await CreateTaskAsync(pm.Client, projectB, "Của B");

        var page = await QueryAsync(pm.Client, projectA, new TaskQueryRequest());

        page.Items.ShouldHaveSingleItem().Task.Id.ShouldBe(inA);
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Sap_xep_theo_uu_tien_va_phan_trang_giu_dung_tong()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await CreateTaskAsync(pm.Client, projectId, "Thấp",  priority: Priority.Lowest);
        await CreateTaskAsync(pm.Client, projectId, "Cao",   priority: Priority.Highest);
        await CreateTaskAsync(pm.Client, projectId, "Vừa",   priority: Priority.Medium);

        var page = await QueryAsync(pm.Client, projectId,
            new TaskQueryRequest(SortBy: TaskField.Priority, PageSize: 2));

        page.TotalCount.ShouldBe(3);
        page.Items.Count.ShouldBe(2);
        // Priority.Highest = 0 nên tăng dần = gấp nhất trước.
        page.Items[0].Task.Priority.ShouldBe(Priority.Highest);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_khong_truy_van_duoc()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await outsider.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/tasks/query", new TaskQueryRequest());

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
