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
    private readonly IProjectInvitationRepository _invitationRepo = Substitute.For<IProjectInvitationRepository>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IAppLinkBuilder _linkBuilder = Substitute.For<IAppLinkBuilder>();

    private readonly Guid _pmId = Guid.NewGuid();
    private readonly ProjectMemberService _sut;

    public ProjectMemberServiceTests()
    {
        _uow.Projects.Returns(_projectRepo);
        _uow.Employees.Returns(_employeeRepo);
        _uow.Tasks.Returns(_taskRepo);
        _uow.ProjectMembers.Returns(_memberRepo);
        _uow.ProjectInvitations.Returns(_invitationRepo);
        _currentUser.EmployeeId.Returns(_pmId);

        // Mọi test đều có thể gọi InviteExternalAsync/AcceptExternalInvitationAsync, cả hai
        // đều load "actor"/"current employee" bằng GetByIdAsync(currentUser.EmployeeId) —
        // stub sẵn một Employee mặc định để không phải lặp lại ở từng test không quan tâm actor.
        _employeeRepo.GetByIdAsync(_pmId, Arg.Any<CancellationToken>()).Returns(Emp(_pmId, "PM test"));

        _tokenService.CreateSecureToken().Returns("raw-token");
        _tokenService.HashToken(Arg.Any<string>()).Returns(ci => "hash-of-" + ci.Arg<string>());
        _linkBuilder.BuildInvitationLink(Arg.Any<string>())
            .Returns(ci => $"https://pms.test/invitations/{ci.Arg<string>()}");

        _sut = new ProjectMemberService(
            _uow, _authz, _currentUser, _activityLog, _notifications,
            new ProjectMapper(), NullLogger<ProjectMemberService>.Instance,
            _tokenService, _emailSender, _linkBuilder);
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

    // ---------- InviteExternalAsync ----------

    [Fact]
    public async Task InviteExternalAsync_tu_moi_chinh_minh_thi_400()
    {
        var project = ProjectOf();
        var me = Emp(_pmId, "PM test");
        _employeeRepo.GetByIdAsync(_pmId, Arg.Any<CancellationToken>()).Returns(me);

        await Should.ThrowAsync<BusinessRuleException>(() => _sut.InviteExternalAsync(
            project.Id, new InviteExternalRequest(me.Email, RoleInProject.Member)));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteExternalAsync_email_da_la_thanh_vien_thi_409()
    {
        var memberId = Guid.NewGuid();
        var project = ProjectOf((memberId, RoleInProject.Member, InvitationStatus.Accepted));
        var member = project.Members.First(m => m.EmployeeId == memberId);
        _employeeRepo.GetByEmailAsync(member.Employee.Email, Arg.Any<CancellationToken>())
                     .Returns(member.Employee);

        await Should.ThrowAsync<ConflictException>(() => _sut.InviteExternalAsync(
            project.Id, new InviteExternalRequest(member.Employee.Email, RoleInProject.Member)));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteExternalAsync_email_chua_co_tai_khoan_van_tao_duoc_loi_moi_va_gui_email()
    {
        var project = ProjectOf();
        _employeeRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                     .Returns((Employee?)null);

        var result = await _sut.InviteExternalAsync(
            project.Id, new InviteExternalRequest("nguoi.la@ngoai.test", RoleInProject.Member));

        result.Email.ShouldBe("nguoi.la@ngoai.test");
        result.ProjectId.ShouldBe(project.Id);

        await _invitationRepo.Received(1).AddAsync(
            Arg.Is<ProjectInvitation>(i => i!.Email == "nguoi.la@ngoai.test"), Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendAsync(
            "nguoi.la@ngoai.test", Arg.Any<string>(),
            Arg.Is<string>(body => body!.Contains("https://pms.test/invitations/raw-token")),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteExternalAsync_moi_lai_email_dang_Pending_thi_vo_hieu_loi_moi_cu()
    {
        var project = ProjectOf();
        _employeeRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                     .Returns((Employee?)null);

        var oldInvitation = ProjectInvitation.Create(
            project.Id, "nguoi.la@ngoai.test", RoleInProject.Viewer, _pmId,
            "hash-cu", DateTime.UtcNow.AddDays(5), null);
        _invitationRepo.GetPendingByProjectAndEmailAsync(
            project.Id, "nguoi.la@ngoai.test", Arg.Any<CancellationToken>())
            .Returns(oldInvitation);

        await _sut.InviteExternalAsync(
            project.Id, new InviteExternalRequest("nguoi.la@ngoai.test", RoleInProject.Member));

        oldInvitation.IsUsable.ShouldBeFalse();
    }

    // ---------- GetInvitationPreviewAsync / AcceptExternalInvitationAsync ----------

    private ProjectInvitation Invitation(
        Guid projectId, string email, RoleInProject role, DateTime? expiresAt = null, DateTime? usedAt = null)
    {
        var invitation = ProjectInvitation.Create(
            projectId, email, role, _pmId, "hash-of-raw-token",
            expiresAt ?? DateTime.UtcNow.AddDays(7), null);
        if (usedAt.HasValue) invitation.MarkUsed();
        return invitation;
    }

    [Fact]
    public async Task GetInvitationPreviewAsync_token_khong_ton_tai_hoac_het_han_thi_cung_mot_loi()
    {
        _invitationRepo.GetByHashAsync("hash-of-raw-token", Arg.Any<CancellationToken>())
                       .Returns((ProjectInvitation?)null);

        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.GetInvitationPreviewAsync("raw-token"));
    }

    [Fact]
    public async Task GetInvitationPreviewAsync_token_hop_le_thi_tra_thong_tin_project()
    {
        var project = ProjectOf();
        var invitation = Invitation(project.Id, "ai.do@ngoai.test", RoleInProject.Member);
        invitation.Project = project;
        _invitationRepo.GetByHashAsync("hash-of-raw-token", Arg.Any<CancellationToken>())
                       .Returns(invitation);

        var preview = await _sut.GetInvitationPreviewAsync("raw-token");

        preview.ProjectName.ShouldBe(project.Name);
        preview.Email.ShouldBe("ai.do@ngoai.test");
    }

    [Fact]
    public async Task AcceptExternalInvitationAsync_email_khong_khop_thi_403()
    {
        var project = ProjectOf();
        var invitation = Invitation(project.Id, "email.duoc.moi@ngoai.test", RoleInProject.Member);
        invitation.Project = project;
        _invitationRepo.GetByHashAsync("hash-of-raw-token", Arg.Any<CancellationToken>())
                       .Returns(invitation);

        var caller = Emp(_pmId, "Người khác");   // email KHÁC invitation.Email
        _employeeRepo.GetByIdAsync(_pmId, Arg.Any<CancellationToken>()).Returns(caller);

        await Should.ThrowAsync<ForbiddenException>(
            () => _sut.AcceptExternalInvitationAsync("raw-token"));
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptExternalInvitationAsync_email_khop_thi_them_thanh_vien_va_tieu_token()
    {
        var project = ProjectOf();
        var caller = Emp(_pmId, "PM test");   // caller CHÍNH LÀ creator/PM sẵn có trong project
        var invitation = Invitation(project.Id, caller.Email, RoleInProject.Viewer);
        invitation.Project = project;
        _invitationRepo.GetByHashAsync("hash-of-raw-token", Arg.Any<CancellationToken>())
                       .Returns(invitation);
        _employeeRepo.GetByIdAsync(_pmId, Arg.Any<CancellationToken>()).Returns(caller);

        // Caller đã là PM Accepted (do ProjectOf tạo creator) -> nhánh idempotent: chỉ tiêu
        // token, KHÔNG gọi AddMember lần hai.
        var result = await _sut.AcceptExternalInvitationAsync("raw-token");

        result.RoleInProject.ShouldBe(RoleInProject.ProjectManager);   // vai trò cũ giữ nguyên, không bị Viewer ghi đè
        invitation.IsUsed.ShouldBeTrue();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptExternalInvitationAsync_nguoi_moi_thi_tao_member_Accepted_ngay()
    {
        var project = ProjectOf();
        var inviteeId = Guid.NewGuid();
        var caller = Emp(inviteeId, "Người ngoài");
        var invitation = Invitation(project.Id, caller.Email, RoleInProject.Member);
        invitation.Project = project;
        _invitationRepo.GetByHashAsync("hash-of-raw-token", Arg.Any<CancellationToken>())
                       .Returns(invitation);
        _currentUser.EmployeeId.Returns(inviteeId);
        _employeeRepo.GetByIdAsync(inviteeId, Arg.Any<CancellationToken>()).Returns(caller);

        var result = await _sut.AcceptExternalInvitationAsync("raw-token");

        result.InvitationStatus.ShouldBe(InvitationStatus.Accepted);
        result.RoleInProject.ShouldBe(RoleInProject.Member);
        invitation.IsUsed.ShouldBeTrue();

        _notifications.Received(1).NotifyMany(
            Arg.Any<IEnumerable<Guid>>(), NotificationType.InvitationAccepted,
            Arg.Any<string>(), project.Id);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
