using PMS.Domain.Common;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Domain;

public class ProjectMemberTests
{
    private readonly Guid _creatorId = Guid.NewGuid();

    private Project NewProject() =>
        Project.Create("PMS", "Mô tả", DateTime.UtcNow.AddDays(30), _creatorId);

    private static Employee NewEmployee(string name = "Nguyen Van B") =>
        new() { Id = Guid.NewGuid(), Name = name, Email = $"{Guid.NewGuid():N}@pms.test" };

    // ---------- Vòng đời lời mời ----------

    [Fact]
    public void Invite_tao_loi_moi_Pending_va_chua_co_JoinedDate()
    {
        var project = NewProject();
        var b = NewEmployee();

        var member = project.Invite(b, RoleInProject.Member);

        member.InvitationStatus.ShouldBe(InvitationStatus.Pending);
        member.JoinedDate.ShouldBeNull();
        project.GetRoleOf(b.Id).ShouldBeNull();   // Pending chưa tính là thành viên
    }

    [Fact]
    public void Accept_dong_dau_JoinedDate_va_kich_hoat_thanh_vien()
    {
        var project = NewProject();
        var b = NewEmployee();
        var member = project.Invite(b, RoleInProject.Member);

        member.Accept();

        member.InvitationStatus.ShouldBe(InvitationStatus.Accepted);
        member.JoinedDate.ShouldNotBeNull();
        project.GetRoleOf(b.Id).ShouldBe(RoleInProject.Member);
    }

    [Fact]
    public void Decline_khong_dong_dau_JoinedDate()
    {
        var project = NewProject();
        var member = project.Invite(NewEmployee(), RoleInProject.Member);

        member.Decline();

        member.InvitationStatus.ShouldBe(InvitationStatus.Declined);
        member.JoinedDate.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]   // Accept rồi Accept lại
    [InlineData(false)]  // Decline rồi Decline lại
    public void Phan_hoi_loi_moi_lan_hai_bi_chan(bool accept)
    {
        var project = NewProject();
        var member = project.Invite(NewEmployee(), RoleInProject.Member);
        if (accept) member.Accept(); else member.Decline();

        // Chống double-click / replay request ở tầng domain, không phụ thuộc tầng trên
        Should.Throw<DomainException>(() => { if (accept) member.Accept(); else member.Decline(); });
    }

    // ---------- Mời trùng / mời lại ----------

    [Fact]
    public void Invite_nguoi_dang_Pending_bi_chan()
    {
        var project = NewProject();
        var b = NewEmployee();
        project.Invite(b, RoleInProject.Member);

        Should.Throw<DomainException>(() => project.Invite(b, RoleInProject.Viewer));
    }

    [Fact]
    public void Invite_nguoi_da_Accepted_bi_chan()
    {
        var project = NewProject();
        var b = NewEmployee();
        project.Invite(b, RoleInProject.Member).Accept();

        Should.Throw<DomainException>(() => project.Invite(b, RoleInProject.Member));
    }

    [Fact]
    public void Invite_lai_nguoi_da_Declined_thi_reset_row_cu_chu_khong_tao_row_moi()
    {
        var project = NewProject();
        var b = NewEmployee();
        project.Invite(b, RoleInProject.Viewer).Decline();

        var member = project.Invite(b, RoleInProject.Member);

        member.InvitationStatus.ShouldBe(InvitationStatus.Pending);
        member.RoleInProject.ShouldBe(RoleInProject.Member);   // role của lời mời MỚI

        // Điểm mấu chốt: chỉ 2 row (creator + b). Tạo row thứ 3 sẽ vỡ
        // unique index (ProjectId, EmployeeId) khi SaveChanges.
        project.Members.Count.ShouldBe(2);
    }

    // ---------- Invariant: luôn còn ít nhất 1 PM ----------

    [Fact]
    public void Ha_vai_tro_cua_PM_duy_nhat_bi_chan()
    {
        var project = NewProject();

        var ex = Should.Throw<DomainException>(
            () => project.ChangeMemberRole(_creatorId, RoleInProject.Member));

        ex.Message.ShouldContain("Project Manager");
        // Chặn TRƯỚC khi mutate -> entity không bị bẩn trong ChangeTracker
        project.GetRoleOf(_creatorId).ShouldBe(RoleInProject.ProjectManager);
    }

    [Fact]
    public void Go_PM_duy_nhat_bi_chan()
    {
        var project = NewProject();

        Should.Throw<DomainException>(() => project.RemoveMember(_creatorId));
        project.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void Con_PM_khac_thi_ha_vai_tro_thanh_cong()
    {
        var project = NewProject();
        var b = NewEmployee();
        project.Invite(b, RoleInProject.ProjectManager).Accept();

        project.ChangeMemberRole(_creatorId, RoleInProject.Member);

        project.GetRoleOf(_creatorId).ShouldBe(RoleInProject.Member);
        project.GetRoleOf(b.Id).ShouldBe(RoleInProject.ProjectManager);
    }

    [Fact]
    public void PM_thu_hai_dang_Pending_khong_duoc_tinh_la_PM_dang_hoat_dong()
    {
        var project = NewProject();
        project.Invite(NewEmployee(), RoleInProject.ProjectManager);   // KHÔNG Accept

        // EnsureAnotherManagerExists lọc theo IsActive() -> Pending không cứu được
        Should.Throw<DomainException>(() => project.RemoveMember(_creatorId));
    }

    [Fact]
    public void Doi_vai_tro_thanh_chinh_no_la_no_op_khong_nem_loi()
    {
        var project = NewProject();

        Should.NotThrow(() => project.ChangeMemberRole(_creatorId, RoleInProject.ProjectManager));
        project.GetRoleOf(_creatorId).ShouldBe(RoleInProject.ProjectManager);
    }

    [Fact]
    public void Thao_tac_tren_nguoi_khong_phai_thanh_vien_bi_chan()
    {
        var project = NewProject();
        var nguoiLa = Guid.NewGuid();

        Should.Throw<DomainException>(() => project.RemoveMember(nguoiLa));
        Should.Throw<DomainException>(() => project.ChangeMemberRole(nguoiLa, RoleInProject.Member));
    }
}