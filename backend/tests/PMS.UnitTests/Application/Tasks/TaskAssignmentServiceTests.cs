using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Tasks;

public class TaskAssignmentServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IRepository<TaskAssignment> _assignmentRepo = Substitute.For<IRepository<TaskAssignment>>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private readonly Guid _pmId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Project _project;
    private readonly TaskAssignmentService _sut;

    public TaskAssignmentServiceTests()
    {
        _uow.Tasks.Returns(_taskRepo);
        _uow.Projects.Returns(_projectRepo);
        _uow.TaskAssignments.Returns(_assignmentRepo);
        _currentUser.EmployeeId.Returns(_actorId);

        _project = Project.Create("PMS", "Mô tả", DateTime.UtcNow.AddDays(30), _pmId);
        foreach (var m in _project.Members)
            m.Employee = new Employee { Id = m.EmployeeId, Name = "PM", Email = "pm@pms.test" };

        _projectRepo.GetWithMembersAsync(_project.Id, Arg.Any<CancellationToken>()).Returns(_project);

        _sut = new TaskAssignmentService(
            _uow, _authz, _currentUser, _activityLog, _notifications,
            new TaskMapper(),
            NullLogger<TaskAssignmentService>.Instance);
    }

    // ---------- AssignAsync (seq-02) ----------

    [Fact]
    public async Task AssignAsync_yeu_cau_quyen_ManageAssignees()
    {
        var target = AddAcceptedMember(RoleInProject.Member);
        var task = ArrangeTask();

        await _sut.AssignAsync(task.Id, new AssignTaskRequest(target.Id, RoleInTask.Owner));

        await _authz.Received(1).AuthorizeAsync(
            _project.Id, ProjectAction.ManageAssignees, Arg.Any<CancellationToken>());
        task.Assignments.ShouldHaveSingleItem().EmployeeId.ShouldBe(target.Id);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_target_khong_phai_thanh_vien_project_thi_403()
    {
        // seq-02: nhánh "target not member" -> 403, chặn ở service vì TaskItem không có
        // nav property tới ProjectMember.
        var task = ArrangeTask();

        await Should.ThrowAsync<ForbiddenException>(
            () => _sut.AssignAsync(task.Id, new AssignTaskRequest(Guid.NewGuid(), RoleInTask.Owner)));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_target_moi_duoc_moi_chua_Accept_thi_403()
    {
        var pending = new Employee { Id = Guid.NewGuid(), Name = "Chưa accept", Email = "p@pms.test" };
        var member = _project.Invite(pending, RoleInProject.Member);
        member.Employee = pending;                     // Pending, chưa Accept
        var task = ArrangeTask();

        await Should.ThrowAsync<ForbiddenException>(
            () => _sut.AssignAsync(task.Id, new AssignTaskRequest(pending.Id, RoleInTask.Owner)));
    }

    [Fact]
    public async Task AssignAsync_gan_trung_nguoi_thi_409()
    {
        var target = AddAcceptedMember(RoleInProject.Member);
        var task = ArrangeTask();
        task.AddAssignee(target, RoleInTask.Owner);

        await Should.ThrowAsync<ConflictException>(
            () => _sut.AssignAsync(task.Id, new AssignTaskRequest(target.Id, RoleInTask.Contributor)));
    }

    [Fact]
    public async Task AssignAsync_bao_cho_dung_nguoi_duoc_gan()
    {
        var target = AddAcceptedMember(RoleInProject.Member);
        var task = ArrangeTask();

        await _sut.AssignAsync(task.Id, new AssignTaskRequest(target.Id, RoleInTask.Owner));

        _activityLog.Received(1).Log(
            nameof(TaskItem), task.Id, ActivityAction.Assigned, Arg.Any<string>());
        _notifications.Received(1).Notify(
            target.Id, NotificationType.TaskAssigned, Arg.Any<string>(), task.Id);
    }

    // ---------- SelfAssignAsync ----------

    [Fact]
    public async Task SelfAssignAsync_dung_quyen_SelfAssign_chu_khong_phai_ManageAssignees()
    {
        AddAcceptedMember(RoleInProject.Member, _actorId);
        var task = ArrangeTask();

        await _sut.SelfAssignAsync(task.Id);

        await _authz.Received(1).AuthorizeAsync(
            _project.Id, ProjectAction.SelfAssign, Arg.Any<CancellationToken>());
        task.Assignments.ShouldHaveSingleItem().EmployeeId.ShouldBe(_actorId);
    }

    [Fact]
    public async Task SelfAssignAsync_task_khong_o_ToDo_thi_409()
    {
        AddAcceptedMember(RoleInProject.Member, _actorId);
        var task = ArrangeTask();
        task.ChangeStatus(Status.InProgress);

        var ex = await Should.ThrowAsync<ConflictException>(() => _sut.SelfAssignAsync(task.Id));
        ex.Message.ShouldContain("ToDo");

        task.Assignments.ShouldBeEmpty();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelfAssignAsync_nhan_lai_task_da_nhan_thi_409()
    {
        var self = AddAcceptedMember(RoleInProject.Member, _actorId);
        var task = ArrangeTask();
        task.AddAssignee(self, RoleInTask.Owner);

        await Should.ThrowAsync<ConflictException>(() => _sut.SelfAssignAsync(task.Id));
    }

    [Fact]
    public async Task SelfAssignAsync_bao_cho_PM_de_PM_nam_duoc_ai_dang_lam_gi()
    {
        AddAcceptedMember(RoleInProject.Member, _actorId);
        var task = ArrangeTask();

        await _sut.SelfAssignAsync(task.Id);

        _notifications.Received(1).NotifyMany(
            Arg.Is<IEnumerable<Guid>>(ids => ids != null && ids.Contains(_pmId)),
            NotificationType.TaskAssigned, Arg.Any<string>(), task.Id);
    }

    // ---------- UnassignAsync ----------

    [Fact]
    public async Task UnassignAsync_tu_rut_chi_can_quyen_SelfAssign()
    {
        var self = AddAcceptedMember(RoleInProject.Member, _actorId);
        var task = ArrangeTask();
        task.AddAssignee(self, RoleInTask.Owner);
        var assignment = task.Assignments.Single();

        await _sut.UnassignAsync(task.Id, _actorId);

        await _authz.Received(1).AuthorizeAsync(
            _project.Id, ProjectAction.SelfAssign, Arg.Any<CancellationToken>());
        // Xóa cứng tường minh, không dựa vào orphan-cascade ngầm của EF (ADR-008)
        _assignmentRepo.Received(1).Remove(assignment);
        task.Assignments.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnassignAsync_go_nguoi_khac_can_quyen_ManageAssignees()
    {
        var target = AddAcceptedMember(RoleInProject.Member);
        var task = ArrangeTask();
        task.AddAssignee(target, RoleInTask.Owner);

        await _sut.UnassignAsync(task.Id, target.Id);

        await _authz.Received(1).AuthorizeAsync(
            _project.Id, ProjectAction.ManageAssignees, Arg.Any<CancellationToken>());
        _notifications.Received(1).Notify(
            target.Id, NotificationType.TaskUnassigned, Arg.Any<string>(), task.Id);
    }

    [Fact]
    public async Task UnassignAsync_nguoi_von_khong_duoc_gan_thi_404()
    {
        var target = AddAcceptedMember(RoleInProject.Member);
        var task = ArrangeTask();

        await Should.ThrowAsync<NotFoundException>(() => _sut.UnassignAsync(task.Id, target.Id));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- helpers ----------

    private Employee AddAcceptedMember(RoleInProject role, Guid? id = null)
    {
        var employee = new Employee
        {
            Id = id ?? Guid.NewGuid(), Name = "Nhân sự", Email = $"{Guid.NewGuid():N}@pms.test"
        };
        var member = _project.Invite(employee, role);
        member.Employee = employee;
        member.Accept();
        return employee;
    }

    private TaskItem ArrangeTask()
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Name = "Task test",
            ProjectId = _project.Id, ReporterId = _pmId
        };
        _taskRepo.GetWithAssignmentsAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        return task;
    }
}
