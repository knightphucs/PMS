using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Projects;

public class ProjectMemberService : IProjectMemberService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly ProjectMapper _mapper;
    private readonly ILogger<ProjectMemberService> _logger;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IAppLinkBuilder _linkBuilder;

    /// <summary>Lời mời qua email hết hạn sau 7 ngày — dài hơn token đặt lại mật khẩu (30 phút, ADR-041)
    /// vì người ngoài hệ thống cần thời gian để nhận email, đăng ký tài khoản rồi mới bấm chấp nhận.</summary>
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public ProjectMemberService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, INotificationService notifications,
        ProjectMapper mapper, ILogger<ProjectMemberService> logger,
        ITokenService tokenService, IEmailSender emailSender, IAppLinkBuilder linkBuilder)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _linkBuilder = linkBuilder;
    }

    public async Task<IReadOnlyList<ProjectMemberResponse>> GetMembersAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var project = await LoadProjectAsync(projectId, ct);

        return project.Members.Select(_mapper.ToMemberResponse).ToList();
    }

    public async Task<ProjectMemberResponse> InviteAsync(
        Guid projectId, InviteMemberRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageMembers, ct);

        var email = request.Email.Trim();

        var invitee = await _uow.Employees.GetByEmailAsync(email, ct)
            ?? throw new NotFoundException(
                $"Email '{email}' chưa có tài khoản trong hệ thống. " +
                "Người này cần đăng ký trước khi được mời vào project.");

        if (invitee.Id == _currentUser.RequireEmployeeId())
            throw new BusinessRuleException("Không thể tự mời chính mình vào project.");

        var project = await LoadProjectAsync(projectId, ct);

        // Thành viên được thêm trực tiếp để có thể cộng tác ngay; vẫn chỉ chọn được tài
        // khoản đã tồn tại trong hệ thống, nên không có đường thêm email lạ vào project.
        var member = project.AddMember(invitee, request.Role);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.MemberInvited,
            $"Thêm {invitee.Name} ({invitee.Email}) với vai trò {request.Role}");

        _notifications.Notify(invitee.Id, NotificationType.InvitedToProject,
            $"Bạn đã được thêm vào project '{project.Name}' với vai trò {request.Role}",
            projectId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Thêm {InviteeId} vào project {ProjectId} bởi {ActorId}",
            invitee.Id, projectId, _currentUser.EmployeeId);

        // member vừa được tạo trong bộ nhớ nên navigation Employee còn null ->
        // mapper đọc member.Employee.Name sẽ NRE. Gán sau SaveChanges để EF không
        // hiểu nhầm là muốn insert Employee mới.
        member.Employee = invitee;
        return _mapper.ToMemberResponse(member);
    }

    public Task<ProjectMemberResponse> AcceptInvitationAsync(
        Guid projectId, CancellationToken ct = default)
        => RespondToInvitationAsync(projectId, accept: true, ct);

    public Task<ProjectMemberResponse> DeclineInvitationAsync(
        Guid projectId, CancellationToken ct = default)
        => RespondToInvitationAsync(projectId, accept: false, ct);

    public async Task<ProjectMemberResponse> ChangeRoleAsync(
        Guid projectId, Guid employeeId, ChangeMemberRoleRequest request,
        CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageMembers, ct);

        var project = await LoadProjectAsync(projectId, ct);
        var member = RequireMember(project, employeeId);
        var oldRole = member.RoleInProject;

        if (oldRole == request.Role)
            return _mapper.ToMemberResponse(member);   // không đổi gì -> không log, không notify

        if (request.Role == RoleInProject.Viewer)
            await EnsureNoActiveTasksAsync(projectId, employeeId, "hạ vai trò xuống Viewer", ct);

        project.ChangeMemberRole(employeeId, request.Role);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.MemberRoleChanged,
            $"Đổi vai trò của {member.Employee.Name}: {oldRole} -> {request.Role}");

        _notifications.Notify(employeeId, NotificationType.RoleChanged,
            $"Vai trò của bạn trong project '{project.Name}' đã đổi thành {request.Role}",
            projectId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Đổi vai trò {EmployeeId} trong project {ProjectId}: {OldRole} -> {NewRole} bởi {ActorId}",
            employeeId, projectId, oldRole, request.Role, _currentUser.EmployeeId);

        return _mapper.ToMemberResponse(member);
    }

    public async Task RemoveMemberAsync(
        Guid projectId, Guid employeeId, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();
        var isSelfLeave = employeeId == actorId;

        // Một endpoint, hai nhánh quyền: tự rời chỉ cần là thành viên; gỡ người khác cần PM.
        // Người đang Pending gọi vào đây sẽ nhận 404 từ AuthorizeAsync (chưa Accepted nên
        // chưa có role) — đúng ý: họ phải dùng /decline chứ không phải "rời project".
        await _authz.AuthorizeAsync(
            projectId, isSelfLeave ? ProjectAction.View : ProjectAction.ManageMembers, ct);

        var project = await LoadProjectAsync(projectId, ct);
        var member = RequireMember(project, employeeId);
        var memberName = member.Employee.Name;
        var removedRole = member.RoleInProject;

        await EnsureNoActiveTasksAsync(
            projectId, employeeId, isSelfLeave ? "rời project" : "gỡ khỏi project", ct);

        project.RemoveMember(employeeId);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.MemberRemoved,
            isSelfLeave
                ? $"{memberName} tự rời project (vai trò {removedRole})"
                : $"Gỡ {memberName} khỏi project (vai trò {removedRole})");

        if (isSelfLeave)
            _notifications.NotifyMany(ActiveManagerIds(project),
                NotificationType.MemberLeftProject,
                $"{memberName} đã rời project '{project.Name}'", projectId);
        else
            _notifications.Notify(employeeId, NotificationType.RemovedFromProject,
                $"Bạn đã bị gỡ khỏi project '{project.Name}'", projectId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Gỡ {EmployeeId} khỏi project {ProjectId} bởi {ActorId} (selfLeave={SelfLeave})",
            employeeId, projectId, actorId, isSelfLeave);
    }

    public async Task<IReadOnlyList<MyInvitationResponse>> GetMyInvitationsAsync(
        CancellationToken ct = default)
    {
        var invitations = await _uow.ProjectMembers.GetPendingInvitationsAsync(
            _currentUser.RequireEmployeeId(), ct);

        return invitations.Select(_mapper.ToMyInvitation).ToList();
    }

    private async Task<ProjectMemberResponse> RespondToInvitationAsync(
        Guid projectId, bool accept, CancellationToken ct)
    {
        var employeeId = _currentUser.RequireEmployeeId();

        var project = await LoadProjectAsync(projectId, ct);

        var member = project.Members.FirstOrDefault(m => m.EmployeeId == employeeId)
            ?? throw new NotFoundException("Không tìm thấy lời mời tham gia project này.");

        // Endpoint cũ vẫn có thể được client gọi sau POST /members. Thành viên nay được
        // thêm ngay lập tức, vì vậy accept lần nữa là no-op để tương thích ngược.
        if (accept && member.IsActive())
            return _mapper.ToMemberResponse(member);

        // Trạng thái != Pending -> DomainException = 409 (chống double-click / replay request)
        if (accept) member.Accept();
        else        member.Decline();

        var action  = accept ? ActivityAction.MemberJoined : ActivityAction.MemberDeclined;
        var verb    = accept ? "chấp nhận" : "từ chối";
        var notiType = accept ? NotificationType.InvitationAccepted
                              : NotificationType.InvitationDeclined;

        _activityLog.Log(nameof(Project), projectId, action,
            $"{member.Employee.Name} đã {verb} lời mời (vai trò {member.RoleInProject})");

        _notifications.NotifyMany(ActiveManagerIds(project), notiType,
            $"{member.Employee.Name} đã {verb} lời mời tham gia project '{project.Name}'",
            projectId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("{EmployeeId} {Verb} lời mời vào project {ProjectId}",
            employeeId, verb, projectId);

        return _mapper.ToMemberResponse(member);
    }

    public async Task<ExternalInvitationResponse> InviteExternalAsync(
        Guid projectId, InviteExternalRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageMembers, ct);

        var email = request.Email.Trim();
        var actor = await _uow.Employees.GetByIdAsync(_currentUser.RequireEmployeeId(), ct)
            ?? throw new NotFoundException(nameof(Employee), _currentUser.RequireEmployeeId());

        if (string.Equals(actor.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Không thể tự mời chính mình vào project.");

        var project = await LoadProjectAsync(projectId, ct);

        // Nếu email này đã ứng với một tài khoản VÀ đã là thành viên (active hoặc đang chờ
        // lời mời trong-app cũ) thì chặn ở đây — dùng nút "Đổi vai trò" thay vì mời lại.
        var existingEmployee = await _uow.Employees.GetByEmailAsync(email, ct);
        if (existingEmployee is not null
            && project.Members.Any(m => m.EmployeeId == existingEmployee.Id))
            throw new ConflictException(
                "Người này đã là thành viên hoặc đang có lời mời chờ phản hồi trong project.");

        // Lời mời Pending cũ cho cùng email trong project này -> vô hiệu, coi như mời lại
        // (resend) thay vì để tồn tại song song hai token cùng sống.
        var existingInvitation = await _uow.ProjectInvitations.GetPendingByProjectAndEmailAsync(
            projectId, email, ct);
        existingInvitation?.Invalidate();

        var rawToken = _tokenService.CreateSecureToken();
        var tokenHash = _tokenService.HashToken(rawToken);
        var expiresAt = DateTime.UtcNow.Add(InvitationLifetime);

        var invitation = ProjectInvitation.Create(
            projectId, email, request.Role, actor.Id, tokenHash, expiresAt,
            _currentUser.IpAddress);

        await _uow.ProjectInvitations.AddAsync(invitation, ct);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.MemberInvited,
            $"Mời {email} qua email với vai trò {request.Role}");

        await _uow.SaveChangesAsync(ct);

        var link = _linkBuilder.BuildInvitationLink(rawToken);
        await _emailSender.SendAsync(
            email,
            $"Lời mời tham gia project '{project.Name}'",
            $"{actor.Name} đã mời bạn tham gia project '{project.Name}' trên PMS với vai trò {request.Role}.\n" +
            $"Bấm vào link sau để tham gia: {link}\n" +
            $"Link có hiệu lực trong {InvitationLifetime.TotalDays:0} ngày. " +
            "Nếu bạn chưa có tài khoản, hãy đăng ký bằng đúng email này.\n\n" +
            "Nếu không nhận ra lời mời này, bạn có thể bỏ qua email.",
            ct);

        _logger.LogInformation(
            "Mời {Email} vào project {ProjectId} qua email bởi {ActorId}", email, projectId, actor.Id);

        return new ExternalInvitationResponse(invitation.Id, projectId, email, request.Role, expiresAt);
    }

    public async Task<InvitationPreviewResponse> GetInvitationPreviewAsync(
        string rawToken, CancellationToken ct = default)
    {
        var invitation = await RequireUsableInvitationAsync(rawToken, ct);

        return new InvitationPreviewResponse(
            invitation.ProjectId, invitation.Project.Name, invitation.Email,
            invitation.Role, invitation.ExpiresAt);
    }

    public async Task<ProjectMemberResponse> AcceptExternalInvitationAsync(
        string rawToken, CancellationToken ct = default)
    {
        var invitation = await RequireUsableInvitationAsync(rawToken, ct);

        var currentEmployee = await _uow.Employees.GetByIdAsync(_currentUser.RequireEmployeeId(), ct)
            ?? throw new NotFoundException(nameof(Employee), _currentUser.RequireEmployeeId());

        if (!invitation.IsForEmail(currentEmployee.Email))
            throw new ForbiddenException(
                "Lời mời này dành cho một email khác. Hãy đăng nhập đúng tài khoản được mời, " +
                "hoặc đăng ký tài khoản mới bằng email đã nhận lời mời.");

        var project = await LoadProjectAsync(invitation.ProjectId, ct);

        // Idempotent: đã là thành viên active rồi (ví dụ bấm accept ở hai tab) thì chỉ tiêu
        // token, không gọi AddMember lần hai (sẽ ném DomainException).
        var member = project.Members.FirstOrDefault(m => m.EmployeeId == currentEmployee.Id);
        if (member is null || !member.IsActive())
        {
            member = project.AddMember(currentEmployee, invitation.Role);

            _activityLog.Log(nameof(Project), project.Id, ActivityAction.MemberJoined,
                $"{currentEmployee.Name} ({currentEmployee.Email}) đã chấp nhận lời mời qua email " +
                $"(vai trò {invitation.Role})");

            _notifications.NotifyMany(ActiveManagerIds(project), NotificationType.InvitationAccepted,
                $"{currentEmployee.Name} đã tham gia project '{project.Name}' qua lời mời email",
                project.Id);

            member.Employee = currentEmployee;
        }

        invitation.MarkUsed();

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "{EmployeeId} chấp nhận lời mời email vào project {ProjectId}",
            currentEmployee.Id, project.Id);

        return _mapper.ToMemberResponse(member);
    }

    /// <summary>
    /// Hash token và tra ra lời mời — hỏng vì lý do gì (không tồn tại / hết hạn / đã dùng)
    /// cũng ra CÙNG MỘT lỗi, để không biến endpoint này thành công cụ dò token (mirror
    /// nguyên tắc gộp lỗi của <c>AuthService.ResetPasswordAsync</c>, ADR-041).
    /// </summary>
    private async Task<ProjectInvitation> RequireUsableInvitationAsync(
        string rawToken, CancellationToken ct)
    {
        var hash = _tokenService.HashToken(rawToken);
        var invitation = await _uow.ProjectInvitations.GetByHashAsync(hash, ct);

        if (invitation is null || !invitation.IsUsable)
            throw new BusinessRuleException("Lời mời không hợp lệ hoặc đã hết hạn.");

        return invitation;
    }

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken ct)
        => await _uow.Projects.GetWithMembersAsync(projectId, ct)
           ?? throw new NotFoundException(nameof(Project), projectId);

    private static ProjectMember RequireMember(Project project, Guid employeeId)
        => project.Members.FirstOrDefault(m => m.EmployeeId == employeeId)
           ?? throw new NotFoundException("Nhân sự này không có trong danh sách thành viên của project.");

    private static IEnumerable<Guid> ActiveManagerIds(Project project)
        => project.Members
                  .Where(m => m.RoleInProject == RoleInProject.ProjectManager && m.IsActive())
                  .Select(m => m.EmployeeId);

    private async Task EnsureNoActiveTasksAsync(
        Guid projectId, Guid employeeId, string actionName, CancellationToken ct)
    {
        var activeCount = await _uow.Tasks.CountActiveAssignedAsync(projectId, employeeId, ct);

        if (activeCount > 0)
            throw new ConflictException(
                $"Không thể {actionName} khi còn {activeCount} task chưa hoàn thành đang được gán. " +
                "Hãy chuyển giao hoặc hoàn thành các task đó trước.");
    }
}
