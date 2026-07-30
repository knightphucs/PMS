using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Application.Features.Comments;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Comments;

public class CommentServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICommentRepository _commentRepo = Substitute.For<ICommentRepository>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly IEmployeeRepository _employeeRepo = Substitute.For<IEmployeeRepository>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _taskId = Guid.NewGuid();
    private readonly CommentService _sut;

    public CommentServiceTests()
    {
        _uow.Comments.Returns(_commentRepo);
        _uow.Tasks.Returns(_taskRepo);
        _uow.Employees.Returns(_employeeRepo);
        _currentUser.EmployeeId.Returns(_actorId);

        _employeeRepo.GetByIdAsync(_actorId, Arg.Any<CancellationToken>())
                     .Returns(NewEmployee(_actorId, "Nguyen Van A"));

        _sut = new CommentService(
            _uow, _authz, _currentUser, _activityLog, _notifications,
            new CommentMapper(),
            NullLogger<CommentService>.Instance);
    }

    // ---------- ADR-019: quyền viết đi qua ProjectAction.CreateComment ----------

    [Fact]
    public async Task Viet_comment_yeu_cau_quyen_CreateComment_va_sinh_Id_phia_app()
    {
        var task = NewTask();
        _taskRepo.GetWithNotificationTargetsAsync(_taskId, Arg.Any<CancellationToken>()).Returns(task);

        Comment? captured = null;
        _commentRepo.When(r => r.Add(Arg.Any<Comment>()))
                    .Do(call => captured = call.Arg<Comment>());

        await _sut.CreateAsync(_taskId, new CreateCommentRequest("  Nội dung  "));

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.CreateComment, Arg.Any<CancellationToken>());

        captured.ShouldNotBeNull();
        captured.Id.ShouldNotBe(Guid.Empty);   // ApplyIdNeverGenerated (bài học 2026-07-30)
        captured.TaskId.ShouldBe(_taskId);
        captured.EmployeeId.ShouldBe(_actorId);
        captured.Content.ShouldBe("Nội dung");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());   // ADR-007
    }

    [Fact]
    public async Task Doc_comment_chi_can_quyen_View_de_Viewer_van_doc_duoc()
    {
        _taskRepo.GetByIdAsync(_taskId, Arg.Any<CancellationToken>()).Returns(NewTask());
        _commentRepo.GetPagedByTaskAsync(_taskId, Arg.Any<PagedRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new PagedResult<Comment> { Items = [], Page = 1, PageSize = 20 });

        await _sut.GetByTaskAsync(_taskId, new PagedRequest());

        await _authz.Received(1).AuthorizeAsync(
            _projectId, ProjectAction.View, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Comment_moi_thong_bao_cho_assignee_watcher_va_reporter()
    {
        // Dùng lại TaskNotificationExtensions của luồng đổi trạng thái — không có bản sao
        // thứ hai để lệch dần.
        var task = NewTask();
        var assigneeId = Guid.NewGuid();
        var watcherId = Guid.NewGuid();
        task.Assignments.Add(new TaskAssignment { Id = Guid.NewGuid(), TaskId = _taskId, EmployeeId = assigneeId });
        task.Watchers.Add(new Watcher { TaskId = _taskId, EmployeeId = watcherId });
        _taskRepo.GetWithNotificationTargetsAsync(_taskId, Arg.Any<CancellationToken>()).Returns(task);

        await _sut.CreateAsync(_taskId, new CreateCommentRequest("Nội dung"));

        _notifications.Received(1).NotifyMany(
            Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(assigneeId) && ids.Contains(watcherId) && ids.Contains(task.ReporterId)),
            NotificationType.CommentAdded,
            Arg.Any<string>(),
            _taskId);
    }

    [Fact]
    public async Task Comment_moi_ghi_ActivityLog()
    {
        // ADR-013 chấp nhận rủi ro "có thể quên gọi logger" -> mỗi action phải có test.
        _taskRepo.GetWithNotificationTargetsAsync(_taskId, Arg.Any<CancellationToken>()).Returns(NewTask());

        await _sut.CreateAsync(_taskId, new CreateCommentRequest("Nội dung"));

        _activityLog.Received(1).Log(
            nameof(TaskItem), _taskId, ActivityAction.Commented, Arg.Any<string>());
    }

    // ---------- ADR-026: sửa chỉ tác giả, xóa tác giả hoặc PM ----------

    [Fact]
    public async Task Chi_tac_gia_moi_sua_duoc_comment()
    {
        var comment = NewComment(authorId: _actorId);
        _commentRepo.GetWithTaskAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.Member);

        var result = await _sut.UpdateAsync(comment.Id, new UpdateCommentRequest("  Sửa lại  "));

        result.Content.ShouldBe("Sửa lại");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectManager_cung_khong_sua_duoc_comment_cua_nguoi_khac()
    {
        // Xóa lời người khác là kiểm duyệt hợp lý của PM; VIẾT LẠI lời người khác thì không,
        // vì nội dung vẫn đứng tên tác giả cũ.
        var comment = NewComment(authorId: Guid.NewGuid());
        _commentRepo.GetWithTaskAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.ProjectManager);

        await Should.ThrowAsync<ForbiddenException>(
            () => _sut.UpdateAsync(comment.Id, new UpdateCommentRequest("Sửa lén")));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tac_gia_xoa_duoc_comment_cua_minh()
    {
        var comment = NewComment(authorId: _actorId);
        _commentRepo.GetWithTaskAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.Member);

        await _sut.DeleteAsync(comment.Id);

        _commentRepo.Received(1).Remove(comment);   // xóa CỨNG, nhất quán ADR-012
        _activityLog.Received(1).Log(
            nameof(TaskItem), _taskId, ActivityAction.CommentDeleted, Arg.Any<string>());
    }

    [Fact]
    public async Task ProjectManager_xoa_duoc_comment_cua_nguoi_khac()
    {
        var comment = NewComment(authorId: Guid.NewGuid());
        _commentRepo.GetWithTaskAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.ProjectManager);

        await _sut.DeleteAsync(comment.Id);

        _commentRepo.Received(1).Remove(comment);
    }

    [Fact]
    public async Task Member_khong_xoa_duoc_comment_cua_nguoi_khac()
    {
        var comment = NewComment(authorId: Guid.NewGuid());
        _commentRepo.GetWithTaskAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.Member);

        await Should.ThrowAsync<ForbiddenException>(() => _sut.DeleteAsync(comment.Id));

        _commentRepo.DidNotReceive().Remove(Arg.Any<Comment>());
    }

    // ---------- ADR-019: chuẩn hóa 404 ----------

    [Fact]
    public async Task Comment_khong_ton_tai_thi_404()
        => await Should.ThrowAsync<NotFoundException>(() => _sut.DeleteAsync(Guid.NewGuid()));

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_cua_Comment_chu_khong_phai_cua_Project()
    {
        var comment = NewComment(authorId: Guid.NewGuid());
        _commentRepo.GetWithTaskAsync(comment.Id, Arg.Any<CancellationToken>()).Returns(comment);
        _authz.AuthorizeAsync(_projectId, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns<RoleInProject>(_ => throw new NotFoundException(nameof(Project), _projectId));

        var ex = await Should.ThrowAsync<NotFoundException>(() => _sut.DeleteAsync(comment.Id));

        ex.Message.ShouldContain(comment.Id.ToString());
        ex.Message.ShouldNotContain(_projectId.ToString());
    }

    [Fact]
    public async Task Task_khong_ton_tai_thi_khong_tao_duoc_comment()
        => await Should.ThrowAsync<NotFoundException>(
            () => _sut.CreateAsync(Guid.NewGuid(), new CreateCommentRequest("Nội dung")));

    // ---------- helpers ----------

    private TaskItem NewTask() => new()
    {
        Id = _taskId, Name = "Dựng API",
        ProjectId = _projectId, ReporterId = Guid.NewGuid()
    };

    private Comment NewComment(Guid authorId) => new()
    {
        Id = Guid.NewGuid(),
        TaskId = _taskId,
        EmployeeId = authorId,
        Content = "Nội dung gốc",
        Task = NewTask(),
        Author = NewEmployee(authorId, "Tran Thi B")
    };

    private static Employee NewEmployee(Guid id, string name)
    {
        var employee = Employee.Register(name, $"{id:N}@pms.test", "hash");
        employee.Id = id;
        return employee;
    }
}
