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

    public ProjectMemberService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, INotificationService notifications,
        ProjectMapper mapper, ILogger<ProjectMemberService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
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