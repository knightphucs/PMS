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

public class TaskServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISprintRepository _sprintRepo = Substitute.For<ISprintRepository>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IProjectTaskCounterRepository _counterRepo = Substitute.For<IProjectTaskCounterRepository>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private const string ProjectKey = "PMS";
    private readonly TaskService _sut;

    public TaskServiceTests()
    {
        _uow.Tasks.Returns(_taskRepo);
        _uow.Sprints.Returns(_sprintRepo);
        _uow.Projects.Returns(_projectRepo);
        _uow.ProjectTaskCounters.Returns(_counterRepo);
        _currentUser.EmployeeId.Returns(_userId);

        _projectRepo.GetKeyAsync(_projectId, Arg.Any<CancellationToken>()).Returns(ProjectKey);

        // Mặc định cấp số 1. Test nào quan tâm tới việc đánh số thì override.
        _counterRepo.NextNumberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(1);

        // ExecuteInTransactionAsync nhận delegate; substitute mặc định KHÔNG gọi nó, nên
        // toàn bộ thân CreateAsync sẽ im lặng không chạy và mọi assert đều rỗng. Phải bảo
        // substitute thực thi delegate để test kiểm đúng thứ code thật làm.
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<Task>>()!.Invoke());

        _sut = new TaskService(
            _uow, _authz, _currentUser, _activityLog,
            new TaskMapper(),
            NullLogger<TaskService>.Instance);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_yeu_cau_quyen_CreateTask_va_dat_Reporter_la_nguoi_goi()
    {
        TaskItem? captured = null;
        await _taskRepo.AddAsync(Arg.Do<TaskItem>(t => captured = t));

        await _sut.CreateAsync(NewRequest());

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.CreateTask, Arg.Any<CancellationToken>());

        captured.ShouldNotBeNull();
        captured.Id.ShouldNotBe(Guid.Empty);          // BaseEntity không tự sinh Id
        captured.ReporterId.ShouldBe(_userId);        // Reporter lấy từ JWT, không cho client khai
        captured.Status.ShouldBe(Status.ToDo);        // task mới luôn bắt đầu ở ToDo (seq-01)
        captured.Name.ShouldBe("Task mới");           // đã trim

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_co_ParentTaskId_thi_thanh_subtask_va_thua_ke_ProjectId()
    {
        var parent = NewTask();
        _taskRepo.GetWithSubtasksAsync(parent.Id, Arg.Any<CancellationToken>()).Returns(parent);

        var result = await _sut.CreateAsync(NewRequest() with { ParentTaskId = parent.Id });

        result.ParentTaskId.ShouldBe(parent.Id);
        parent.Subtasks.ShouldHaveSingleItem().ProjectId.ShouldBe(parent.ProjectId);
    }

    [Fact]
    public async Task CreateAsync_subtask_cua_subtask_bi_domain_chan()
    {
        var parent = NewTask();
        var child = new TaskItem { Id = Guid.NewGuid(), Name = "Subtask" };
        parent.AddSubtask(child);
        _taskRepo.GetWithSubtasksAsync(child.Id, Arg.Any<CancellationToken>()).Returns(child);

        // DomainException -> 409, không phải 500 (ADR-011)
        await Should.ThrowAsync<DomainException>(
            () => _sut.CreateAsync(NewRequest() with { ParentTaskId = child.Id }));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_task_cha_o_project_khac_thi_bi_chan_400()
    {
        // FK không chặn được: TaskItem giữ ProjectId và ParentTaskId độc lập nhau.
        var parent = NewTask();
        parent.ProjectId = Guid.NewGuid();
        _taskRepo.GetWithSubtasksAsync(parent.Id, Arg.Any<CancellationToken>()).Returns(parent);

        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.CreateAsync(NewRequest() with { ParentTaskId = parent.Id }));
    }

    [Fact]
    public async Task CreateAsync_sprint_o_project_khac_thi_bi_chan_400()
    {
        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint lạ", ProjectId = Guid.NewGuid() };
        _sprintRepo.GetByIdAsync(sprint.Id, Arg.Any<CancellationToken>()).Returns(sprint);

        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.CreateAsync(NewRequest() with { SprintId = sprint.Id }));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- ADR-033/034: đánh số task và mã hiển thị ----------

    [Fact]
    public async Task CreateAsync_lay_so_tu_bo_dem_va_ghep_ma_dang_KEY_so()
    {
        _counterRepo.NextNumberAsync(_projectId, Arg.Any<CancellationToken>()).Returns(42);

        var result = await _sut.CreateAsync(NewRequest());

        result.Number.ShouldBe(42);
        result.Code.ShouldBe("PMS-42");
    }

    [Fact]
    public async Task CreateAsync_cap_so_BEN_TRONG_transaction_chu_khong_phai_ngoai()
    {
        // Đây là điểm mấu chốt của ADR-033: câu UPDATE...OUTPUT chỉ giữ được X lock tới
        // hết transaction. Gọi ngoài transaction thì lock nhả ngay sau câu lệnh và hai
        // người tạo task cùng lúc có thể nhận cùng một số. Test này đỏ nếu ai đó "dọn dẹp"
        // bằng cách bỏ ExecuteInTransactionAsync đi.
        var insideTransaction = false;
        _counterRepo.NextNumberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => { insideTransaction = true; return 1; });

        _uow.ExecuteInTransactionAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                insideTransaction.ShouldBeFalse("phải lấy số BÊN TRONG transaction");
                await call.Arg<Func<Task>>()!.Invoke();
                insideTransaction.ShouldBeTrue();
            });

        await _sut.CreateAsync(NewRequest());

        await _uow.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_mo_ta_toan_khoang_trang_luu_thanh_null()
    {
        TaskItem? captured = null;
        await _taskRepo.AddAsync(Arg.Do<TaskItem>(t => captured = t));

        await _sut.CreateAsync(NewRequest() with { Description = "   " });

        // "" và null cùng nghĩa "chưa có mô tả"; lưu cả hai thì frontend phải kiểm cả hai.
        captured.ShouldNotBeNull();
        captured.Description.ShouldBeNull();
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsync_ghi_de_concurrency_token_bang_gia_tri_client_gui_len()
    {
        var task = NewTaskWithReporter();
        _taskRepo.GetWithDetailsAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        byte[] clientRowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        await _sut.UpdateAsync(task.Id,
            new UpdateTaskRequest("  Tên mới  ", null, Priority.High, clientRowVersion));

        task.Name.ShouldBe("Tên mới");
        // ADR-016: không ghi đè original value thì concurrency check không bao giờ kích hoạt
        _uow.Received(1).SetConcurrencyToken(task, clientRowVersion);
        _taskRepo.DidNotReceive().Update(Arg.Any<TaskItem>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_yeu_cau_quyen_UpdateTask_chu_khong_phai_View()
    {
        var task = NewTaskWithReporter();
        _taskRepo.GetWithDetailsAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        await _sut.UpdateAsync(task.Id,
            new UpdateTaskRequest("Tên", null, Priority.Low, [1, 2, 3]));

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.UpdateTask, Arg.Any<CancellationToken>());
    }

    // ---------- DeleteAsync (ADR-018) ----------

    [Fact]
    public async Task DeleteAsync_con_subtask_chua_Done_thi_nem_Conflict_va_khong_luu_gi()
    {
        var task = NewTask();
        task.AddSubtask(SubtaskAt(Status.InProgress));
        _taskRepo.GetWithSubtasksAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var ex = await Should.ThrowAsync<ConflictException>(() => _sut.DeleteAsync(task.Id));
        ex.Message.ShouldContain("1");   // message phải nêu số subtask đang chặn

        _taskRepo.DidNotReceive().Remove(Arg.Any<TaskItem>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_moi_subtask_da_Done_thi_cascade_tuong_minh_xuong_subtask()
    {
        var task = NewTask();
        var subtask = SubtaskAt(Status.Done);
        task.AddSubtask(subtask);
        _taskRepo.GetWithSubtasksAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        await _sut.DeleteAsync(task.Id);

        // Cascade phải viết tay: ApplySoftDelete() đổi state Deleted -> Modified nên
        // cascade tự động của EF Core không kích hoạt (ADR-008).
        _taskRepo.Received(1).Remove(subtask);
        _taskRepo.Received(1).Remove(task);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_task_khong_ton_tai_thi_404()
        => await Should.ThrowAsync<NotFoundException>(() => _sut.DeleteAsync(Guid.NewGuid()));

    // ---------- MoveToSprintAsync ----------

    [Fact]
    public async Task MoveToSprintAsync_SprintId_null_thi_dua_task_ve_Backlog()
    {
        var task = NewTask();
        task.SprintId = Guid.NewGuid();
        _taskRepo.GetWithSubtasksAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        var result = await _sut.MoveToSprintAsync(task.Id, new MoveTaskToSprintRequest(null));

        task.SprintId.ShouldBeNull();
        result.SprintId.ShouldBeNull();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveToSprintAsync_yeu_cau_quyen_ManageSprint()
    {
        var task = NewTask();
        _taskRepo.GetWithSubtasksAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        await _sut.MoveToSprintAsync(task.Id, new MoveTaskToSprintRequest(null));

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.ManageSprint, Arg.Any<CancellationToken>());
    }

    // ---------- GetBoardAsync ----------

    [Fact]
    public async Task GetBoardAsync_luon_tra_du_4_cot_ke_ca_cot_rong()
    {
        _taskRepo.GetRootTasksByProjectAsync(_projectId, Arg.Any<CancellationToken>())
                 .Returns([TaskAt(Status.ToDo)]);

        var board = await _sut.GetBoardAsync(_projectId, null);

        board.Columns.Count.ShouldBe(4);
        board.Columns.Select(c => c.Status).ShouldBe(Enum.GetValues<Status>());
        board.Columns.Single(c => c.Status == Status.ToDo).Tasks.Count.ShouldBe(1);
        board.Columns.Single(c => c.Status == Status.Done).Tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBoardAsync_theo_sprint_thi_loai_subtask_khoi_board()
    {
        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint 1", ProjectId = _projectId };
        _sprintRepo.GetByIdAsync(sprint.Id, Arg.Any<CancellationToken>()).Returns(sprint);

        var root = TaskAt(Status.ToDo);
        var subtask = SubtaskAt(Status.ToDo);
        subtask.ParentTaskId = Guid.NewGuid();
        _taskRepo.GetBySprintAsync(sprint.Id, Arg.Any<CancellationToken>())
                 .Returns([root, subtask]);

        var board = await _sut.GetBoardAsync(_projectId, sprint.Id);

        board.SprintId.ShouldBe(sprint.Id);
        board.Columns.SelectMany(c => c.Tasks).ShouldHaveSingleItem().Id.ShouldBe(root.Id);
    }

    [Fact]
    public async Task GetBoardAsync_sprint_o_project_khac_thi_bi_chan_400()
    {
        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint lạ", ProjectId = Guid.NewGuid() };
        _sprintRepo.GetByIdAsync(sprint.Id, Arg.Any<CancellationToken>()).Returns(sprint);

        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.GetBoardAsync(_projectId, sprint.Id));
    }

    // ---------- helpers ----------

    private CreateTaskRequest NewRequest() => new(
        Name: "  Task mới  ", ProjectId: _projectId,
        SprintId: null, ParentTaskId: null, DueDate: null, Priority: Priority.Medium);

    private TaskItem NewTask()
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Name = "Task test",
            ProjectId = _projectId, ReporterId = _userId
        };
        task.AssignNumber(1);
        return task;
    }

    /// <summary>
    /// ToDetail map Reporter.Name và Project.Key nên cả hai navigation phải có sẵn
    /// (ở prod do <c>GetWithDetailsAsync</c> Include nạp).
    /// </summary>
    private TaskItem NewTaskWithReporter()
    {
        var task = NewTask();
        task.Reporter = new Employee { Id = _userId, Name = "Reporter", Email = "r@pms.test" };
        task.Project = new Project { Id = _projectId, Name = "Project test", Key = ProjectKey };
        return task;
    }

    private TaskItem TaskAt(Status status)
    {
        var task = NewTask();
        Advance(task, status);
        return task;
    }

    private static TaskItem SubtaskAt(Status status)
    {
        var subtask = new TaskItem { Id = Guid.NewGuid(), Name = "Subtask" };
        Advance(subtask, status);
        return subtask;
    }

    private static void Advance(TaskItem task, Status target)
    {
        Status[] path = target switch
        {
            Status.ToDo       => [],
            Status.InProgress => [Status.InProgress],
            Status.Review     => [Status.InProgress, Status.Review],
            Status.Done       => [Status.InProgress, Status.Review, Status.Done],
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        foreach (var step in path) task.ChangeStatus(step);
    }
}
