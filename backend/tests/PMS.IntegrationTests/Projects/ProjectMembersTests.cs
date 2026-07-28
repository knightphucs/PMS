using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
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

    [Fact] // KB17 — luồng chính end-to-end
    public async Task Moi_roi_chap_nhan_thi_thanh_vien_kich_hoat_day_du()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var invite = await pm.Client.PostAsJsonAsync(
            $"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest(invitee.Email, RoleInProject.Member));

        invite.StatusCode.ShouldBe(HttpStatusCode.Created);
        var pending = await invite.Content.ReadFromJsonAsync<ProjectMemberResponse>();
        pending!.InvitationStatus.ShouldBe(InvitationStatus.Pending);
        pending.JoinedDate.ShouldBeNull();

        // Người được mời thấy lời mời trong hộp của mình
        var box = await invitee.Client.GetFromJsonAsync<List<MyInvitationResponse>>(
            "/api/v1/Projects/invitations");
        box!.ShouldHaveSingleItem().ProjectId.ShouldBe(projectId);

        var accept = await invitee.Client.PostAsync(
            $"/api/v1/Projects/{projectId}/members/me/accept", null);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        var joined = await accept.Content.ReadFromJsonAsync<ProjectMemberResponse>();
        joined!.InvitationStatus.ShouldBe(InvitationStatus.Accepted);
        joined.JoinedDate.ShouldNotBeNull();

        // Đã là thành viên -> project hiện trong danh sách của họ
        var members = await invitee.Client.GetFromJsonAsync<List<ProjectMemberResponse>>(
            $"/api/v1/Projects/{projectId}/members");
        members!.Count.ShouldBe(2);

        // Hộp lời mời rỗng trở lại
        (await invitee.Client.GetFromJsonAsync<List<MyInvitationResponse>>(
            "/api/v1/Projects/invitations"))!.ShouldBeEmpty();
    }

    [Fact] // KB18 — ADR-013: log và notification phải cùng transaction với dữ liệu
    public async Task Moi_va_chap_nhan_deu_sinh_ActivityLog_va_Notification()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await InviteAndAcceptAsync(pm.Client, invitee, projectId, RoleInProject.Member);

        var actions = await WithDbAsync(db => db.ActivityLogs
            .Where(l => l.EntityId == projectId)
            .Select(l => l.Action).ToListAsync());

        actions.ShouldContain(ActivityAction.MemberInvited);
        actions.ShouldContain(ActivityAction.MemberJoined);

        var notiTypes = await WithDbAsync(db => db.Notifications
            .Where(n => n.RelatedEntityId == projectId)
            .Select(n => new { n.EmployeeId, n.Type }).ToListAsync());

        notiTypes.ShouldContain(x =>
            x.EmployeeId == invitee.EmployeeId && x.Type == NotificationType.InvitedToProject);
        notiTypes.ShouldContain(x =>
            x.EmployeeId == pm.EmployeeId && x.Type == NotificationType.InvitationAccepted);

        // NotifyMany loại người đang thao tác -> invitee không tự nhận thông báo mình đã accept
        notiTypes.ShouldNotContain(x =>
            x.EmployeeId == invitee.EmployeeId && x.Type == NotificationType.InvitationAccepted);
    }

    [Fact] // KB19
    public async Task Member_thuong_khong_duoc_moi_nguoi_khac()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var nguoiMoi = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

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
    public async Task Tu_choi_roi_moi_lai_thi_chi_co_dung_mot_hang_trong_DB()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var body = new InviteMemberRequest(invitee.Email, RoleInProject.Member);

        await pm.Client.PostAsJsonAsync($"/api/v1/Projects/{projectId}/members", body);
        (await invitee.Client.PostAsync(
            $"/api/v1/Projects/{projectId}/members/me/decline", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await pm.Client.PostAsJsonAsync($"/api/v1/Projects/{projectId}/members", body))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        // Reset row cũ chứ không insert row mới -> unique index (ProjectId, EmployeeId) không vỡ
        var rows = await WithDbAsync(db => db.ProjectMembers
            .CountAsync(m => m.ProjectId == projectId && m.EmployeeId == invitee.EmployeeId));
        rows.ShouldBe(1);
    }

    [Fact] // KB23
    public async Task Nguoi_khac_khong_the_chap_nhan_ho_loi_moi()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var keXauDaLaThanhVien = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, keXauDaLaThanhVien, projectId, RoleInProject.Member);

        await pm.Client.PostAsJsonAsync($"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest(invitee.Email, RoleInProject.Member));

        // Endpoint chỉ nhận /me -> không có đường truyền employeeId của người khác.
        // Kẻ này đã là thành viên Accepted; gửi lại accept là replay lời mời -> 409.
        (await keXauDaLaThanhVien.Client.PostAsync(
            $"/api/v1/Projects/{projectId}/members/me/accept", null))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var status = await WithDbAsync(db => db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.EmployeeId == invitee.EmployeeId)
            .Select(m => m.InvitationStatus).SingleAsync());
        status.ShouldBe(InvitationStatus.Pending);
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
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

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
    public async Task Loi_moi_vao_project_da_xoa_khong_hien_trong_hop_thu()
    {
        var pm = await CreateUserAsync();
        var invitee = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await pm.Client.PostAsJsonAsync($"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest(invitee.Email, RoleInProject.Member));
        await pm.Client.DeleteAsync($"/api/v1/Projects/{projectId}");

        // Query filter !Project.IsDeleted của ProjectMemberConfiguration lo việc này,
        // service không cần lọc tay.
        (await invitee.Client.GetFromJsonAsync<List<MyInvitationResponse>>(
            "/api/v1/Projects/invitations"))!.ShouldBeEmpty();
    }
}
