using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Sprints;
using PMS.Domain.Common;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Sprints;

public class SprintServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISprintRepository _sprintRepo = Substitute.For<ISprintRepository>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly SprintService _sut;

    public SprintServiceTests()
    {
        _uow.Sprints.Returns(_sprintRepo);
        _currentUser.EmployeeId.Returns(_userId);

        _sut = new SprintService(
            _uow, _authz, _currentUser, _activityLog, _notifications,
            new SprintMapper(),
            NullLogger<SprintService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_yeu_cau_quyen_ManageSprint_va_sinh_Id_phia_app()
    {
        Sprint? captured = null;
        await _sprintRepo.AddAsync(Arg.Do<Sprint>(s => captured = s));

        await _sut.CreateAsync(_projectId, new CreateSprintRequest(
            "  Sprint 1  ", "  Mục tiêu  ", DateTime.UtcNow, DateTime.UtcNow.AddDays(14)));

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.ManageSprint, Arg.Any<CancellationToken>());

        captured.ShouldNotBeNull();
        captured.Id.ShouldNotBe(Guid.Empty);
        captured.ProjectId.ShouldBe(_projectId);
        captured.Name.ShouldBe("Sprint 1");
        captured.Goal.ShouldBe("Mục tiêu");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByProjectAsync_chi_can_quyen_View()
    {
        _sprintRepo.GetByProjectAsync(_projectId, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.GetByProjectAsync(_projectId);

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.View, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TaskCount_dem_dung_so_task_cua_sprint()
    {
        var sprint = NewSprint();
        sprint.Tasks.Add(NewTask());
        sprint.Tasks.Add(NewTask());
        _sprintRepo.GetByProjectAsync(_projectId, Arg.Any<CancellationToken>()).Returns([sprint]);

        var result = await _sut.GetByProjectAsync(_projectId);

        result.ShouldHaveSingleItem().TaskCount.ShouldBe(2);
    }

    // ---------- ADR-020 ----------

    [Fact]
    public async Task DeleteAsync_day_task_ve_Backlog_chu_khong_xoa_task()
    {
        var sprint = NewSprint();
        var task = NewTask();
        task.SprintId = sprint.Id;
        sprint.Tasks.Add(task);
        _sprintRepo.GetWithTasksAsync(sprint.Id, Arg.Any<CancellationToken>()).Returns(sprint);

        await _sut.DeleteAsync(sprint.Id);

        // Bắt buộc null hóa: Sprint là ISoftDeletable nên task trỏ tới sprint đã xóa mềm
        // sẽ có Include(t => t.Sprint) trả null một cách khó hiểu.
        task.SprintId.ShouldBeNull();
        task.IsDeleted.ShouldBeFalse();
        _sprintRepo.Received(1).Remove(sprint);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_yeu_cau_quyen_ManageSprint()
    {
        var sprint = NewSprint();
        _sprintRepo.GetWithTasksAsync(sprint.Id, Arg.Any<CancellationToken>()).Returns(sprint);

        await _sut.DeleteAsync(sprint.Id);

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.ManageSprint, Arg.Any<CancellationToken>());
        _activityLog.Received(1).Log(
            nameof(Project), _projectId, ActivityAction.Deleted, Arg.Any<string>());
    }

    // ---------- ADR-019: chuẩn hóa 404 ----------

    [Fact]
    public async Task Sprint_khong_ton_tai_thi_404()
        => await Should.ThrowAsync<NotFoundException>(() => _sut.GetByIdAsync(Guid.NewGuid()));

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_cua_Sprint_chu_khong_phai_cua_Project()
    {
        var sprint = NewSprint();
        _sprintRepo.GetWithTasksAsync(sprint.Id, Arg.Any<CancellationToken>()).Returns(sprint);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns<RoleInProject>(_ => throw new NotFoundException(nameof(Project), _projectId));

        var ex = await Should.ThrowAsync<NotFoundException>(() => _sut.GetByIdAsync(sprint.Id));

        ex.Message.ShouldContain(sprint.Id.ToString());
        ex.Message.ShouldNotContain(_projectId.ToString());
    }

    // ---------- Vòng đời sprint (ADR-050) ----------

    [Fact]
    public async Task StartAsync_project_da_co_sprint_dang_chay_thi_409()
    {
        // Bất biến: một project tối đa MỘT sprint Active. Không phải luật Scrum cho vui —
        // "sprint hiện tại" là khái niệm mà backlog, board mặc định và velocity đều dựa vào.
        var running = NewSprint();
        running.Start();
        var sprint = ArrangeSprint(ProjectAction.ManageSprint);
        _sprintRepo.GetActiveOfProjectAsync(_projectId, Arg.Any<CancellationToken>())
                   .Returns(running);

        var ex = await Should.ThrowAsync<ConflictException>(() => _sut.StartAsync(sprint.Id));

        ex.Message.ShouldContain(running.Name);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_TargetSprintId_null_thi_day_task_chua_xong_ve_Backlog()
    {
        var sprint = ArrangeSprint(ProjectAction.ManageSprint);
        sprint.Start();
        var chuaXong = TaskInColumn(StatusCategory.InProgress, sprint.Id);
        var daXong = TaskInColumn(StatusCategory.Done, sprint.Id);
        sprint.Tasks.Add(chuaXong);
        sprint.Tasks.Add(daXong);

        await _sut.CompleteAsync(sprint.Id, new CompleteSprintRequest(null));

        // ADR-050: `null` là "về Backlog", một lựa chọn hợp lệ chứ không phải "chưa chọn".
        chuaXong.SprintId.ShouldBeNull();
        // Task đã xong Ở LẠI sprint — đó là thứ velocity đếm. Đẩy nó đi là xóa mất kết quả
        // của chính sprint vừa đóng.
        daXong.SprintId.ShouldBe(sprint.Id);
        sprint.Status.ShouldBe(SprintStatus.Completed);
        sprint.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CompleteAsync_sprint_chua_bat_dau_thi_409()
    {
        var sprint = ArrangeSprint(ProjectAction.ManageSprint);   // vẫn Planned

        await Should.ThrowAsync<DomainException>(
            () => _sut.CompleteAsync(sprint.Id, new CompleteSprintRequest(null)));
    }

    [Fact]
    public async Task CompleteAsync_sprint_dich_thuoc_project_khac_tra_404()
    {
        // 404 chứ không 400: sprint của project khác thì với người gọi nó không tồn tại.
        var sprint = ArrangeSprint(ProjectAction.ManageSprint);
        sprint.Start();
        sprint.Tasks.Add(TaskInColumn(StatusCategory.ToDo, sprint.Id));

        var foreign = new Sprint
        {
            Id = Guid.NewGuid(), Name = "Sprint lạ", ProjectId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(7)
        };
        _sprintRepo.GetWithTasksAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        await Should.ThrowAsync<NotFoundException>(
            () => _sut.CompleteAsync(sprint.Id, new CompleteSprintRequest(foreign.Id)));
    }

    // ---------- helpers ----------

    private Sprint ArrangeSprint(ProjectAction action)
    {
        var sprint = NewSprint();
        _sprintRepo.GetWithTasksAsync(sprint.Id, Arg.Any<CancellationToken>()).Returns(sprint);
        _authz.AuthorizeAsync(_projectId, action, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.ProjectManager);
        return sprint;
    }

    /// <summary>
    /// Task đặt sẵn ở một cột thuộc nhóm cho trước (ADR-052), và ĐANG thuộc sprint.
    ///
    /// <para>
    /// ⚠️ Phải gán <c>SprintId</c> bằng tay: ngoài EF thì việc thêm vào
    /// <c>sprint.Tasks</c> không tự đặt khóa ngoại, nên bỏ qua bước này sẽ khiến mọi task
    /// vào test với <c>SprintId = null</c> — và phép khẳng định "task đã xong Ở LẠI sprint"
    /// sẽ đỏ vì lý do sai.
    /// </para>
    /// </summary>
    private TaskItem TaskInColumn(StatusCategory category, Guid sprintId)
    {
        var task = NewTask();
        task.MoveTo(new BoardColumn
        {
            Id = Guid.NewGuid(), ProjectId = _projectId,
            Name = category.ToString(), Category = category,
        });
        task.SprintId = sprintId;
        return task;
    }

    private Sprint NewSprint() => new()
    {
        Id = Guid.NewGuid(), Name = "Sprint 1", Goal = "Mục tiêu",
        ProjectId = _projectId,
        StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14)
    };

    private TaskItem NewTask() => new()
    {
        Id = Guid.NewGuid(), Name = "Task test",
        ProjectId = _projectId, ReporterId = _userId
    };
}
