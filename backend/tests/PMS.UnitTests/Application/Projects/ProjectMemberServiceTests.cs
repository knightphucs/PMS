using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Projects;
using PMS.Domain.Common;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Projects;

public class ProjectMemberServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IEmployeeRepository _employeeRepo = Substitute.For<IEmployeeRepository>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly IProjectMemberRepository _memberRepo = Substitute.For<IProjectMemberRepository>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private readonly Guid _pmId = Guid.NewGuid();
    private readonly ProjectMemberService _sut;

    public ProjectMemberServiceTests()
    {
        _uow.Projects.Returns(_projectRepo);
        _uow.Employees.Returns(_employeeRepo);
        _uow.Tasks.Returns(_taskRepo);
        _uow.ProjectMembers.Returns(_memberRepo);
        _currentUser.EmployeeId.Returns(_pmId);

        _sut = new ProjectMemberService(
            _uow, _authz, _currentUser, _activityLog, _notifications,
            new ProjectMapper(), NullLogger<ProjectMemberService>.Instance);
    }

    // ---------- Helpers ----------

    private static Employee Emp(Guid id, string name = "Nguyen Van B") =>
        new() { Id = id, Name = name, Email = $"{id:N}@pms.test" };

    /// <summary>Project có creator (_pmId) là PM Accepted, Employee đã gán để mapper không NRE.</summary>
    private Project ProjectOf(params (Guid Id, RoleInProject Role, InvitationStatus Status)[] others)
    {
        var project = Project.Create("PMS", "Mô tả", DateTime.UtcNow.AddDays(30), _pmId, "PMS");

        foreach (var (id, role, status) in others)
        {
            var member = project.Invite(Emp(id), role);
            if (status == InvitationStatus.Accepted) member.Accept();
            if (status == InvitationStatus.Declined) member.Decline();
        }

        foreach (var m in project.Members)
            m.Employee ??= Emp(m.EmployeeId, "PM test");

        _projectRepo.GetWithMembersAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        return project;
    }

    // ---------- InviteAsync ----------

    [Fact]
    public async Task InviteAsync_yeu_cau_quyen_ManageMembers()
    {
        var project = ProjectOf();
        var invitee = Emp(Guid.NewGuid());
        _employeeRepo.GetByEmailAsync(invitee.Email, Arg.Any<CancellationToken>()).Returns(invitee);

        await _sut.InviteAsync(project.Id, new InviteMemberRequest(invitee.Email, RoleInProject.Member));

        await _authz.Received(1).AuthorizeAsync(
            project.Id, ProjectAction.ManageMembers, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteAsync_email_chua_co_tai_khoan_thi_404_va_khong_luu_gi()
    {
        var project = ProjectOf();
        _employeeRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                     .Returns((Employee?)null);

        var ex = await Should.ThrowAsync<NotFoundException>(
            () => _sut.InviteAsync(project.Id, new InviteMemberRequest("ai@do.test", RoleInProject.Member)));

        ex.Message.ShouldContain("đăng ký");   // phải chỉ dẫn được người dùng làm gì tiếp
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteAsync_tu_moi_chinh_minh_thi_400()
    {
        var project = ProjectOf();
        var me = Emp(_pmId);
        _employeeRepo.GetByEmailAsync(me.Email, Arg.Any<CancellationToken>()).Returns(me);

        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.InviteAsync(project.Id, new InviteMemberRequest(me.Email, RoleInProject.Member)));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteAsync_thanh_cong_thi_ghi_log_gui_notification_va_luu_dung_mot_lan()
    {
        var project = ProjectOf();
        var invitee = Emp(Guid.NewGuid(), "Tran Thi C");
        _employeeRepo.GetByEmailAsync(invitee.Email, Arg.Any<CancellationToken>()).Returns(invitee);

        var result = await _sut.InviteAsync(
            project.Id, new InviteMemberRequest($"  {invitee.Email}  ", RoleInProject.Member));

        result.InvitationStatus.ShouldBe(InvitationStatus.Accepted);
        result.JoinedDate.ShouldNotBeNull();
        result.EmployeeName.ShouldBe("Tran Thi C");   // navigation Employee được gán tay sau SaveChanges

        _activityLog.Received(1).Log(
            nameof(Project), project.Id, ActivityAction.MemberInvited, Arg.Any<string>());
        _notifications.Received(1).Notify(
            invitee.Id, NotificationType.InvitedToProject, Arg.Any<string>(), project.Id);

        // ADR-013: log + notification + membership cùng 1 SaveChanges -> cùng 1 transaction
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Phản hồi lời mời ----------

    [Fact]
    public async Task AcceptInvitationAsync_KHONG_goi_AuthorizeAsync()
    {
        var inviteeId = Guid.NewGuid();
        _currentUser.EmployeeId.Returns(inviteeId);
        var project = ProjectOf((inviteeId, RoleInProject.Member, InvitationStatus.Pending));

        await _sut.AcceptInvitationAsync(project.Id);

        // ADR-012 / seq-05: AuthorizeAsync lọc InvitationStatus == Accepted nên người
        // Pending sẽ nhận 404 khi chấp nhận lời mời của CHÍNH MÌNH. Test này khóa lại
        // quyết định thiết kế đó — ai đó "dọn dẹp" bằng cách thêm _authz vào sẽ fail ngay.
        await _authz.DidNotReceiveWithAnyArgs().AuthorizeAsync(default, default, default);
    }

    [Fact]
    public async Task AcceptInvitationAsync_dong_dau_JoinedDate_va_bao_cho_PM()
    {
        var inviteeId = Guid.NewGuid();
        _currentUser.EmployeeId.Returns(inviteeId);
        var project = ProjectOf((inviteeId, RoleInProject.Member, InvitationStatus.Pending));

        var result = await _sut.AcceptInvitationAsync(project.Id);

        result.InvitationStatus.ShouldBe(InvitationStatus.Accepted);
        result.JoinedDate.ShouldNotBeNull();

#pragma warning disable CS8604 // Possible null reference argument.
        _notifications.Received(1).NotifyMany(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(_pmId)),
            NotificationType.InvitationAccepted, Arg.Any<string>(), project.Id);
#pragma warning restore CS8604 // Possible null reference argument.
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptInvitationAsync_khong_co_loi_moi_thi_404()
    {
        _currentUser.EmployeeId.Returns(Guid.NewGuid());   // người lạ
        var project = ProjectOf();

        await Should.ThrowAsync<NotFoundException>(() => _sut.AcceptInvitationAsync(project.Id));
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- ChangeRoleAsync ----------

    [Fact]
    public async Task ChangeRoleAsync_khong_doi_gi_thi_khong_luu_khong_log()
    {
        var memberId = Guid.NewGuid();
        var project = ProjectOf((memberId, RoleInProject.Member, InvitationStatus.Accepted));

        await _sut.ChangeRoleAsync(project.Id, memberId, new ChangeMemberRoleRequest(RoleInProject.Member));

        _activityLog.DidNotReceiveWithAnyArgs().Log(default!, default, default, default!);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeRoleAsync_ha_xuong_Viewer_khi_con_task_chua_Done_thi_409()
    {
        var memberId = Guid.NewGuid();
        var project = ProjectOf((memberId, RoleInProject.Member, InvitationStatus.Accepted));
        _taskRepo.CountActiveAssignedAsync(project.Id, memberId, Arg.Any<CancellationToken>())
                 .Returns(3);

        var ex = await Should.ThrowAsync<ConflictException>(() => _sut.ChangeRoleAsync(
            project.Id, memberId, new ChangeMemberRoleRequest(RoleInProject.Viewer)));

        ex.Message.ShouldContain("3");   // thông báo phải nêu số task đang chặn
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeRoleAsync_nang_len_PM_khong_bi_chan_boi_task_dang_lam()
    {
        var memberId = Guid.NewGuid();
        var project = ProjectOf((memberId, RoleInProject.Member, InvitationStatus.Accepted));
        _taskRepo.CountActiveAssignedAsync(project.Id, memberId, Arg.Any<CancellationToken>())
                 .Returns(5);

        // Chỉ hạ xuống Viewer mới cần guard — Viewer không được gán task.
        // Nâng quyền không làm task mồ côi nên không kiểm tra.
        var result = await _sut.ChangeRoleAsync(
            project.Id, memberId, new ChangeMemberRoleRequest(RoleInProject.ProjectManager));

        result.RoleInProject.ShouldBe(RoleInProject.ProjectManager);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- RemoveMemberAsync ----------

    [Fact]
    public async Task RemoveMemberAsync_go_nguoi_khac_thi_can_quyen_ManageMembers()
    {
        var memberId = Guid.NewGuid();
        var project = ProjectOf((memberId, RoleInProject.Member, InvitationStatus.Accepted));

        await _sut.RemoveMemberAsync(project.Id, memberId);

        await _authz.Received(1).AuthorizeAsync(
            project.Id, ProjectAction.ManageMembers, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveMemberAsync_tu_roi_thi_chi_can_quyen_View()
    {
        var meId = Guid.NewGuid();
        _currentUser.EmployeeId.Returns(meId);
        var project = ProjectOf((meId, RoleInProject.Member, InvitationStatus.Accepted));

        await _sut.RemoveMemberAsync(project.Id, meId);

        // Cùng 1 endpoint nhưng 2 nhánh quyền khác nhau: Member thường phải rời được
        // project của mình mà không cần quyền quản lý thành viên.
        await _authz.Received(1).AuthorizeAsync(
            project.Id, ProjectAction.View, Arg.Any<CancellationToken>());
        await _authz.DidNotReceive().AuthorizeAsync(
            project.Id, ProjectAction.ManageMembers, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveMemberAsync_PM_cuoi_cung_thi_409_tu_domain()
    {
        var project = ProjectOf();

        await Should.ThrowAsync<DomainException>(() => _sut.RemoveMemberAsync(project.Id, _pmId));
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}