using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;

namespace PMS.UnitTests.Domain;

public class ProjectTests
{
    [Fact]
    public void Create_luon_dam_bao_invariant_co_dung_1_ProjectManager_Accepted()
    {
        var creatorId = Guid.NewGuid();

        var project = Project.Create("PMS", "Mô tả", DateTime.UtcNow.AddDays(30), creatorId);

        var pm = project.Members.ShouldHaveSingleItem();
        pm.EmployeeId.ShouldBe(creatorId);
        pm.RoleInProject.ShouldBe(RoleInProject.ProjectManager);
        pm.InvitationStatus.ShouldBe(InvitationStatus.Accepted);
        pm.ProjectId.ShouldBe(project.Id);      // FK gán được vì Id sinh phía app
    }

    [Fact]
    public void Invite_tao_loi_moi_o_trang_thai_Pending()
    {
        var project = Project.Create("PMS", "Mô tả", DateTime.UtcNow.AddDays(30), Guid.NewGuid());
        var invitee = new Employee { Id = Guid.NewGuid(), Name = "B", Email = "b@pms.test" };

        var member = project.Invite(invitee, RoleInProject.Member);

        member.InvitationStatus.ShouldBe(InvitationStatus.Pending);
        member.JoinedDate.ShouldBeNull(); 
        project.GetRoleOf(invitee.Id).ShouldBeNull();
    }

    [Fact]
    public void GetRoleOf_tra_null_cho_nguoi_ngoai_project()
    {
        var project = Project.Create("PMS", "Mô tả", DateTime.UtcNow.AddDays(30), Guid.NewGuid());

        project.GetRoleOf(Guid.NewGuid()).ShouldBeNull();
    }
}