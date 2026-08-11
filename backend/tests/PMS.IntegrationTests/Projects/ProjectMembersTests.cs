using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.Projects;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace PMS.IntegrationTests.Projects;

[Collection(IntegrationTestCollection.Name)]
public class ProjectMembersTests : IntegrationTestBase
{
    public ProjectMembersTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact] // KB17 — luồng chính end-to-end (ADR-057: MỘT bước, không còn chấp nhận)
    public async Task Them_thanh_vien_la_kich_hoat_ngay_trong_mot_buoc()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var added = await pm.Client.PostAsJsonAsync(
            $"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest(invitee.Email, RoleInProject.Member));

        added.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Không còn trạng thái trung gian: phản hồi ĐẦU TIÊN đã là thành viên đầy đủ.
        var joined = await added.Content.ReadFromJsonAsync<ProjectMemberResponse>(TestJson.Options);
        joined!.InvitationStatus.ShouldBe(InvitationStatus.Accepted);
        joined.JoinedDate.ShouldNotBeNull();

        // Và họ truy cập được project ngay, không cần thao tác nào thêm — đây mới là
        // điều KB17 thật sự bảo vệ (trước ADR-057 nó cần hai request mới tới được đây).
        var members = await invitee.Client.GetFromJsonAsync<List<ProjectMemberResponse>>(
            $"/api/v1/Projects/{projectId}/members", TestJson.Options);
        members!.Count.ShouldBe(2);
    }

    [Fact] // KB18 — ADR-013: log và notification phải cùng transaction với dữ liệu
    public async Task Them_thanh_vien_sinh_ActivityLog_va_Notification()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await AddMemberAsync(pm.Client, invitee, projectId, RoleInProject.Member);

        var actions = await WithDbAsync(db => db.ActivityLogs
            .Where(l => l.EntityId == projectId)
            .Select(l => l.Action).ToListAsync());

        actions.ShouldContain(ActivityAction.MemberInvited);

        // ADR-057: không còn bước chấp nhận nên KHÔNG còn dòng MemberJoined. Một hành động
        // của người dùng phải sinh đúng MỘT dòng nhật ký, không phải hai.
        actions.ShouldNotContain(ActivityAction.MemberJoined);

        var notiTypes = await WithDbAsync(db => db.Notifications
            .Where(n => n.RelatedEntityId == projectId)
            .Select(n => new { n.EmployeeId, n.Type }).ToListAsync());

        notiTypes.ShouldContain(x =>
            x.EmployeeId == invitee.EmployeeId && x.Type == NotificationType.InvitedToProject);

        // PM là người vừa BẤM nút thêm — báo lại cho họ chính việc họ vừa làm là tiếng ồn.
        // Trước ADR-057 họ nhận InvitationAccepted vì việc đó do người khác thực hiện.
        notiTypes.ShouldNotContain(x => x.EmployeeId == pm.EmployeeId);
    }

    [Fact] // KB19
    public async Task Member_thuong_khong_duoc_moi_nguoi_khac()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var nguoiMoi = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        var res = await member.Client.PostAsJsonAsync(
            $"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest(nguoiMoi.Email, RoleInProject.Member));

        // Đã là thành viên nhưng role không đủ -> 403 (không phải 404), theo ADR-006
        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact] // KB20
    public async Task Nguoi_ngoai_project_nhan_404_chu_khong_phai_403()
    {
        var pm = await CreateUserAsync();
        var nguoiLa = await CreateUserAsync();
        var muctieu = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await nguoiLa.Client.PostAsJsonAsync(
            $"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest(muctieu.Email, RoleInProject.Member));

        // Không tiết lộ project có tồn tại hay không cho người ngoài
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact] // KB21
    public async Task Moi_trung_thi_409()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var body = new InviteMemberRequest(invitee.Email, RoleInProject.Member);

        (await pm.Client.PostAsJsonAsync($"/api/v1/Projects/{projectId}/members", body))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await pm.Client.PostAsJsonAsync($"/api/v1/Projects/{projectId}/members", body))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact] // KB22
    public async Task Hang_Declined_cu_duoc_RESET_chu_khong_insert_hang_moi()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        // ADR-057 gỡ endpoint decline, nên Declined không còn dựng được qua API. Vẫn phải
        // giữ phép kiểm này: dữ liệu Declined có thật trong DB từ trước ADR-057, và
        // Project.Invite() vẫn phải reset đúng hàng đó thay vì chèn hàng thứ hai — nếu sai
        // thì unique index (ProjectId, EmployeeId) vỡ chứ không phải im lặng sai.
        await SeedMemberAsync(
            projectId, invitee.EmployeeId, RoleInProject.Member, InvitationStatus.Declined);

        (await pm.Client.PostAsJsonAsync($"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest(invitee.Email, RoleInProject.Member)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var rows = await WithDbAsync(db => db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.EmployeeId == invitee.EmployeeId)
            .Select(m => m.InvitationStatus).ToListAsync());

        rows.ShouldHaveSingleItem().ShouldBe(InvitationStatus.Accepted);
    }

    [Fact] // KB23 — biên giới thật sau ADR-057: AI được thêm vào project
    public async Task Them_email_chua_co_tai_khoan_thi_404_va_khong_tao_hang_nao()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        // Thành viên nay Accepted NGAY khi thêm, nên "email nào được phép thêm" chính là
        // chỗ duy nhất còn chặn. Email lạ phải bị từ chối ở đây — không có bước chấp nhận
        // nào phía sau để bắt lại nữa.
        var res = await pm.Client.PostAsJsonAsync($"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest("nguoi-la@khong-ton-tai.test", RoleInProject.Member));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var count = await WithDbAsync(db => db.ProjectMembers
            .CountAsync(m => m.ProjectId == projectId));
        count.ShouldBe(1);   // chỉ còn chính PM
    }

    [Fact] // KB24 — invariant xuyên 4 tầng
    public async Task PM_duy_nhat_khong_the_tu_roi_project()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.DeleteAsync(
            $"/api/v1/Projects/{projectId}/members/{pm.EmployeeId}");

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).ShouldContain("Project Manager");

        var stillThere = await WithDbAsync(db => db.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.EmployeeId == pm.EmployeeId));
        stillThere.ShouldBeTrue();
    }

    [Fact] // KB25
    public async Task Go_thanh_vien_la_xoa_cung_hang_va_ho_mat_quyen_truy_cap()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        (await pm.Client.DeleteAsync(
            $"/api/v1/Projects/{projectId}/members/{member.EmployeeId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // ADR-012: ProjectMember không ISoftDeletable -> xóa cứng, kể cả khi bỏ query filter
        var rows = await WithDbAsync(db => db.ProjectMembers
            .IgnoreQueryFilters()
            .CountAsync(m => m.ProjectId == projectId && m.EmployeeId == member.EmployeeId));
        rows.ShouldBe(0);

        (await member.Client.GetAsync($"/api/v1/Projects/{projectId}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        // Audit trail vẫn còn dù hàng membership đã biến mất
        var logged = await WithDbAsync(db => db.ActivityLogs
            .AnyAsync(l => l.EntityId == projectId && l.Action == ActivityAction.MemberRemoved));
        logged.ShouldBeTrue();
    }

    [Fact] // KB26 — validation đầu vào
    public async Task Role_ngoai_enum_bi_chan_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        // Gửi JSON thô: model binder KHÔNG tự kiểm tra int có thuộc enum không,
        // chỉ IsInEnum() của FluentValidation mới chặn được.
        var res = await pm.Client.PostAsJsonAsync(
            $"/api/v1/Projects/{projectId}/members",
            new { email = "ai@do.test", role = 99 });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact] // KB27
    public async Task Thanh_vien_cua_project_da_xoa_mat_luon_duong_truy_cap()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await AddMemberAsync(pm.Client, invitee, projectId, RoleInProject.Member);
        await pm.Client.DeleteAsync($"/api/v1/Projects/{projectId}");

        // Query filter !Project.IsDeleted của ProjectMemberConfiguration lo việc này,
        // service không cần lọc tay. ADR-057 gỡ hộp thư lời mời, nên điểm quan sát chuyển
        // sang chính đường vào project — cùng một filter, chỗ nhìn thấy được thay đổi.
        (await invitee.Client.GetAsync($"/api/v1/Projects/{projectId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var list = await invitee.Client.GetFromJsonAsync<PagedResult<ProjectSummaryResponse>>(
            "/api/v1/Projects", TestJson.Options);
        list!.Items.ShouldNotContain(p => p.Id == projectId);
    }
}
