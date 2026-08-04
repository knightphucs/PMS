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
    // ViewStatistics — cả ba vai trò (ADR-039). Member được thêm vào 2026-08-03: thống kê
    // là tổng hợp của dữ liệu Member vốn đã đọc được qua /board, không phải đặc quyền.
    [InlineData(ProjectAction.ViewStatistics, RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.ViewStatistics, RoleInProject.Member,         true)]
    [InlineData(ProjectAction.ViewStatistics, RoleInProject.Viewer,         true)]
    // CreateTask / UpdateTask / DeleteTask — chỉ PM (seq-01)
    [InlineData(ProjectAction.CreateTask,      RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.CreateTask,      RoleInProject.Member,         false)]
    [InlineData(ProjectAction.CreateTask,      RoleInProject.Viewer,         false)]
    [InlineData(ProjectAction.UpdateTask,      RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.UpdateTask,      RoleInProject.Member,         false)]
    [InlineData(ProjectAction.UpdateTask,      RoleInProject.Viewer,         false)]
    [InlineData(ProjectAction.DeleteTask,      RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.DeleteTask,      RoleInProject.Member,         false)]
    [InlineData(ProjectAction.DeleteTask,      RoleInProject.Viewer,         false)]
    // ManageAssignees — gán/gỡ NGƯỜI KHÁC, chỉ PM (§5)
    [InlineData(ProjectAction.ManageAssignees, RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.ManageAssignees, RoleInProject.Member,         false)]
    [InlineData(ProjectAction.ManageAssignees, RoleInProject.Viewer,         false)]
    // ManageSprint — chỉ PM
    [InlineData(ProjectAction.ManageSprint,    RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.ManageSprint,    RoleInProject.Member,         false)]
    [InlineData(ProjectAction.ManageSprint,    RoleInProject.Viewer,         false)]
    // SelfAssign — Member cũng được (mô hình Kanban "tự pick up"), Viewer thì không
    [InlineData(ProjectAction.SelfAssign,      RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.SelfAssign,      RoleInProject.Member,         true)]
    [InlineData(ProjectAction.SelfAssign,      RoleInProject.Viewer,         false)]
    // CreateComment — PM + Member, Viewer chỉ đọc (ADR-026)
    [InlineData(ProjectAction.CreateComment,   RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.CreateComment,   RoleInProject.Member,         true)]
    [InlineData(ProjectAction.CreateComment,   RoleInProject.Viewer,         false)]
    // UploadAttachment — soi gương CreateComment (ADR-035)
    [InlineData(ProjectAction.UploadAttachment,  RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.UploadAttachment,  RoleInProject.Member,         true)]
    [InlineData(ProjectAction.UploadAttachment,  RoleInProject.Viewer,         false)]
    // ManageTaskLabels / ManageTaskLinks — PM + Member (ADR-037/038)
    [InlineData(ProjectAction.ManageTaskLabels,  RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.ManageTaskLabels,  RoleInProject.Member,         true)]
    [InlineData(ProjectAction.ManageTaskLabels,  RoleInProject.Viewer,         false)]
    [InlineData(ProjectAction.ManageTaskLinks,   RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.ManageTaskLinks,   RoleInProject.Member,         true)]
    [InlineData(ProjectAction.ManageTaskLinks,   RoleInProject.Viewer,         false)]
    // Watch — cả Viewer, vì nó chỉ ảnh hưởng hộp thông báo của chính người đó (ADR-036)
    [InlineData(ProjectAction.Watch,             RoleInProject.ProjectManager, true)]
    [InlineData(ProjectAction.Watch,             RoleInProject.Member,         true)]
    [InlineData(ProjectAction.Watch,             RoleInProject.Viewer,         true)]
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