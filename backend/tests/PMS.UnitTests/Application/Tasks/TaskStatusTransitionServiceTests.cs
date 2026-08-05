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

public class TaskStatusTransitionServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly IBoardColumnRepository _columnRepo = Substitute.For<IBoardColumnRepository>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    /// <summary>Bốn cột mặc định của project giả, Order 0..3 như <c>BoardColumn.CreateDefaults</c>.</summary>
    private readonly BoardColumn _todo;
    private readonly BoardColumn _doing;
    private readonly BoardColumn _review;
    private readonly BoardColumn _done;

    private readonly TaskStatusTransitionService _sut;

    public TaskStatusTransitionServiceTests()
    {
        _todo   = Column("Cần làm",    0, StatusCategory.ToDo);
        _doing  = Column("Đang làm",   1, StatusCategory.InProgress);
        _review = Column("Đang duyệt", 2, StatusCategory.InProgress);
        _done   = Column("Hoàn thành", 3, StatusCategory.Done);

        _uow.Tasks.Returns(_taskRepo);
        _uow.BoardColumns.Returns(_columnRepo);
        _uow.Projects.Returns(_projectRepo);

        _currentUser.EmployeeId.Returns(_actorId);
        _projectRepo.GetKeyAsync(_projectId, Arg.Any<CancellationToken>()).Returns("PMS");
        _taskRepo.GetUnfinishedBlockersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns([]);

        foreach (var c in new[] { _todo, _doing, _review, _done })
            _columnRepo.GetByIdAsync(c.Id, Arg.Any<CancellationToken>()).Returns(c);

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

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_doing.Id));

        task.BoardColumnId.ShouldBe(_doing.Id);
        task.Category.ShouldBe(StatusCategory.InProgress);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectManager_doi_duoc_status_ca_task_khong_do_minh_lam()
    {
        // Đây là điểm ADR-017 nới rộng so với seq-03 (vốn chỉ vẽ actor "Assignee").
        var task = TaskAssignedTo(Guid.NewGuid());
        Arrange(task, RoleInProject.ProjectManager);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_doing.Id));

        task.BoardColumnId.ShouldBe(_doing.Id);
    }

    [Fact]
    public async Task Member_khong_phai_assignee_bi_tu_choi_403()
    {
        var task = TaskAssignedTo(Guid.NewGuid());
        Arrange(task, RoleInProject.Member);

        await Should.ThrowAsync<ForbiddenException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_doing.Id)));

        task.BoardColumnId.ShouldBe(_todo.Id);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Viewer_bi_tu_choi_403_du_task_khong_co_assignee_nao()
    {
        var task = NewTask();
        Arrange(task, RoleInProject.Viewer);

        await Should.ThrowAsync<ForbiddenException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_doing.Id)));
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
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_doing.Id)));

        ex.Message.ShouldContain(task.Id.ToString());
        ex.Message.ShouldNotContain(_projectId.ToString());
    }

    // ---------- Cột đích (ADR-052) ----------

    // 🗑️ Hai test đã XÓA cùng ADR-052 vì luật chúng khóa không còn tồn tại:
    //   • `Nhay_buoc_ToDo_sang_Done_bi_chan_409` — không còn "bước" nào để nhảy.
    //   • `Doi_hai_lan_toi_cung_mot_dich_thi_lan_hai_bi_chan` — nay là no-op hợp lệ,
    //     xem `Doi_ve_dung_cot_dang_dung_la_no_op` ngay dưới.

    [Fact]
    public async Task Nhay_thang_tu_cot_dau_sang_cot_cuoi_la_hop_le()
    {
        // Trước ADR-052 đây là 409 ("nhảy bước"). Với cột do người dùng tạo thì không còn
        // cơ sở nào để nói cặp nào hợp lệ, nên mọi cột đều tới thẳng được.
        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_done.Id));

        task.Category.ShouldBe(StatusCategory.Done);
    }

    [Fact]
    public async Task Doi_ve_dung_cot_dang_dung_la_no_op_khong_ghi_gi()
    {
        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_todo.Id));

        task.BoardColumnId.ShouldBe(_todo.Id);
        // Không lưu, không ghi log, không bắn thông báo: kéo thẻ về chỗ cũ không phải một
        // sự kiện đáng kể với ai cả.
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _activityLog.DidNotReceive().Log(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ActivityAction>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Cot_cua_project_khac_tra_404_chu_khong_phai_409()
    {
        // 404 chứ không 409: cột của project khác thì với người gọi nó không tồn tại —
        // trả 409 sẽ xác nhận "id này có thật, chỉ là không thuộc project của bạn".
        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);

        var foreign = new BoardColumn
        {
            Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(),
            Name = "Cột lạ", Category = StatusCategory.ToDo,
        };
        _columnRepo.GetByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);

        await Should.ThrowAsync<NotFoundException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(foreign.Id)));
    }

    // ---------- Blocker ----------

    [Fact]
    public async Task Task_bi_chan_boi_task_chua_Done_thi_khong_vao_duoc_nhom_InProgress()
    {
        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);
        _taskRepo.GetUnfinishedBlockersAsync(task.Id, Arg.Any<CancellationToken>())
                 .Returns([new TaskItem { Id = Guid.NewGuid(), Name = "Task chặn" }]);

        var ex = await Should.ThrowAsync<ConflictException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_doing.Id)));

        ex.Message.ShouldContain("Task chặn");
        task.BoardColumnId.ShouldBe(_todo.Id);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Blocker_kiem_theo_NHOM_nen_cot_tu_dat_ten_cung_duoc_bao_ve()
    {
        // Điểm mới của ADR-052: điều kiện là NHÓM của cột đích, không phải tên. Một cột do
        // người dùng đặt tên "Chờ QA" thuộc nhóm InProgress vẫn được guard này bảo vệ.
        var custom = Column("Chờ QA", 4, StatusCategory.InProgress);
        _columnRepo.GetByIdAsync(custom.Id, Arg.Any<CancellationToken>()).Returns(custom);

        var task = TaskAssignedTo(_actorId);
        Arrange(task, RoleInProject.Member);
        _taskRepo.GetUnfinishedBlockersAsync(task.Id, Arg.Any<CancellationToken>())
                 .Returns([new TaskItem { Id = Guid.NewGuid(), Name = "Task chặn" }]);

        await Should.ThrowAsync<ConflictException>(
            () => _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(custom.Id)));
    }

    [Fact]
    public async Task Blocker_khong_kiem_khi_chuyen_sang_nhom_Done()
    {
        var task = TaskAssignedTo(_actorId);
        task.MoveTo(_doing);
        Arrange(task, RoleInProject.Member);

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_done.Id));

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

        await _sut.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(_doing.Id));

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

    private BoardColumn Column(string name, int order, StatusCategory category) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = _projectId,
        Name = name,
        Order = order,
        Category = category,
    };

    /// <summary>Task mới luôn bắt đầu ở cột trái nhất, đúng như <c>TaskService.CreateAsync</c>.</summary>
    private TaskItem NewTask()
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Name = "Task test",
            ProjectId = _projectId, ReporterId = Guid.NewGuid()
        };
        task.MoveTo(_todo);
        return task;
    }

    private TaskItem TaskAssignedTo(Guid employeeId)
    {
        var task = NewTask();
        task.AddAssignee(
            new Employee { Id = employeeId, Name = "Assignee", Email = "a@pms.test" },
            RoleInTask.Owner);
        return task;
    }
}
