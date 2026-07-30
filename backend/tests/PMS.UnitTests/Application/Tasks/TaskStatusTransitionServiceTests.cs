using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Tasks;
using PMS.Domain.Common;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Tasks;

public class TaskStatusTransitionServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly TaskStatusTransitionService _sut;

    public TaskStatusTransitionServiceTests()
    {
        _uow.Tasks.Returns(_taskRepo);
        _currentUser.EmployeeId.Returns(_actorId);
        _taskRepo.GetUnfinishedBlockersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns([]);

        _sut = new TaskStatusTransitionService(
            _uow, _authz, _currentUser, _activityLog, _notifications,
            new TaskMapper(),
            NullLogger<TaskStatusTransitionService>.Instance);
    }

    // ---------- ADR-017: ai được đổi status ----------

    [Fact]
    public async Task Assignee_cua_task_doi_duoc_status()
    {
        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress));

        task.Status.ShouldBe(Status.InProgress);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectManager_doi_duoc_status_ca_task_khong_do_minh_lam()
    {
        // Đây là điểm ADR-017 nới rộng so với seq-03 (vốn chỉ vẽ actor "Assignee").
        var task = TaskAssignedTo(Guid.NewGuid());
        Arrange(task, RoleInProject.ProjectManager);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress));

        task.Status.ShouldBe(Status.InProgress);
    }

    [Fact]
    public async Task Member_khong_phai_assignee_bi_tu_choi_403()
    {
        var task = TaskAssignedTo(Guid.NewGuid());
        Arrange(task, RoleInProject.Member);

        await Should.ThrowAsync<ForbiddenException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress)));

        task.Status.ShouldBe(Status.ToDo);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Viewer_bi_tu_choi_403_du_task_khong_co_assignee_nao()
    {
        var task = NewTask();
        Arrange(task, RoleInProject.Viewer);

        await Should.ThrowAsync<ForbiddenException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress)));
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_cua_task_chu_khong_phai_cua_project()
    {
        // ADR-019: hai trường hợp "task không tồn tại" và "không phải thành viên" phải
        // không phân biệt được nhau qua nội dung lỗi.
        var task = TaskAssignedTo(_actorId);
        _taskRepo.GetForStatusChangeAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns<RoleInProject>(_ => throw new NotFoundException(nameof(Project), _projectId));

        var ex = await Should.ThrowAsync<NotFoundException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress)));

        ex.Message.ShouldContain(task.Id.ToString());
        ex.Message.ShouldNotContain(_projectId.ToString());
    }

    // ---------- Workflow + blocker ----------

    [Fact]
    public async Task Nhay_buoc_ToDo_sang_Done_bi_chan_409()
    {
        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);

        await Should.ThrowAsync<DomainException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.Done)));

        task.Status.ShouldBe(Status.ToDo);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Doi_hai_lan_toi_cung_mot_dich_thi_lan_hai_bi_chan()
    {
        // Chính đặc điểm này là lý do đổi status không cần round-trip RowVersion (ADR-021).
        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress));

        await Should.ThrowAsync<DomainException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress)));
    }

    [Fact]
    public async Task Task_bi_chan_boi_task_chua_Done_thi_khong_vao_duoc_InProgress()
    {
        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);
        _taskRepo.GetUnfinishedBlockersAsync(task.Id, Arg.Any<CancellationToken>())
                 .Returns([new TaskItem { Id = Guid.NewGuid(), Name = "Task chặn" }]);

        var ex = await Should.ThrowAsync<ConflictException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress)));

        ex.Message.ShouldContain("Task chặn");
        task.Status.ShouldBe(Status.ToDo);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Blocker_chi_kiem_khi_chuyen_sang_InProgress()
    {
        var task = TaskAssignedTo(_actorId);
        task.ChangeStatus(Status.InProgress);
        Arrange(task, RoleInProject.Member);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.Review));

        await _taskRepo.DidNotReceive().GetUnfinishedBlockersAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ---------- Log & notification ----------

    [Fact]
    public async Task Ghi_ActivityLog_va_bao_cho_assignee_watcher_reporter()
    {
        var watcherId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var task = TaskAssignedTo(_actorId);
        task.ReporterId = reporterId;
        task.Watchers.Add(new Watcher { TaskId = task.Id, EmployeeId = watcherId });
        Arrange(task, RoleInProject.Member);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(Status.InProgress));

        _activityLog.Received(1).Log(
            nameof(TaskItem), task.Id, ActivityAction.StatusChanged, Arg.Any<string>());

        // NotifyMany tự loại người thực hiện và tự distinct nên service chỉ cần gộp danh sách.
        _notifications.Received(1).NotifyMany(
            Arg.Is<IEnumerable<Guid>>(ids => ids != null
                && ids.Contains(_actorId) && ids.Contains(watcherId) && ids.Contains(reporterId)),
            NotificationType.StatusChanged, Arg.Any<string>(), task.Id);
    }

    // ---------- helpers ----------

    private void Arrange(TaskItem task, RoleInProject role)
    {
        _taskRepo.GetForStatusChangeAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns(role);
    }

    private TaskItem NewTask() => new()
    {
        Id = Guid.NewGuid(), Name = "Task test",
        ProjectId = _projectId, ReporterId = Guid.NewGuid()
    };

    private TaskItem TaskAssignedTo(Guid employeeId)
    {
        var task = NewTask();
        task.AddAssignee(
            new Employee { Id = employeeId, Name = "Assignee", Email = "a@pms.test" },
            RoleInTask.Owner);
        return task;
    }
}
