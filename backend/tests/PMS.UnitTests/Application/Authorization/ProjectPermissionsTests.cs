using PMS.Application.Common.Authorization;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Authorization;

public class ProjectPermissionsTests
{
    [Theory]
    // View — mọi thành viên đã Accepted
    [InlineData(ProjectAction.View,           RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.View,           RoleInProject.Member,         true)]
    [InlineData(ProjectAction.View,           RoleInProject.Viewer,         true)]
    // Update — chỉ PM
    [InlineData(ProjectAction.Update,         RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.Update,         RoleInProject.Member,         false)]
    [InlineData(ProjectAction.Update,         RoleInProject.Viewer,         false)]
    // Delete — chỉ PM
    [InlineData(ProjectAction.Delete,         RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.Delete,         RoleInProject.Member,         false)]
    [InlineData(ProjectAction.Delete,         RoleInProject.Viewer,         false)]
    // ManageMembers — chỉ PM
    [InlineData(ProjectAction.ManageMembers,  RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.ManageMembers,  RoleInProject.Member,         false)]
    [InlineData(ProjectAction.ManageMembers,  RoleInProject.Viewer,         false)]
    // ViewStatistics — PM + Viewer
    [InlineData(ProjectAction.ViewStatistics, RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.ViewStatistics, RoleInProject.Member,         false)]
    [InlineData(ProjectAction.ViewStatistics, RoleInProject.Viewer,         true)]
    public void IsAllowed_khop_voi_ma_tran_trong_tai_lieu(
        ProjectAction action, RoleInProject role, bool expected)
        => ProjectPermissions.IsAllowed(action, role).ShouldBe(expected);

    [Fact]
    public void Moi_gia_tri_ProjectAction_phai_duoc_khai_bao_tuong_minh()
    {
        foreach (var action in Enum.GetValues<ProjectAction>())
            ProjectPermissions.IsAllowed(action, RoleInProject.ProjectManager)
                .ShouldBeTrue($"'{action}' chưa được khai báo trong ProjectPermissions.IsAllowed");
    }
}