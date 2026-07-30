using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Projects;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Projects;

public class ProjectServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly IRepository<Sprint> _sprintRepo = Substitute.For<IRepository<Sprint>>();
    private readonly IProjectAuthorizationService _authz = Substitute.For<IProjectAuthorizationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly ProjectService _sut;   // sut = System Under Test, đối tượng đang kiểm thử

    public ProjectServiceTests()
    {
        _uow.Projects.Returns(_projectRepo);
        _uow.Tasks.Returns(_taskRepo);
        _uow.Sprints.Returns(_sprintRepo);

        _currentUser.EmployeeId.Returns(_userId);

        _sut = new ProjectService(
            _uow, _authz, _currentUser,
            new ProjectMapper(),
            NullLogger<ProjectService>.Instance);
    }

    private static TaskItem TaskWithStatus(Status target)
    {
        var task = new TaskItem { Id = Guid.NewGuid(), Name = "Task test" };

        // Status là private set và chỉ đổi qua ChangeStatus() với state machine
        // ToDo -> InProgress -> Review -> Done. Không nhảy tắt được.
        foreach (var step in PathTo(target))
            task.ChangeStatus(step);

        return task;
    }

    private static Status[] PathTo(Status target) => target switch
    {
        Status.ToDo       => [],
        Status.InProgress => [Status.InProgress],
        Status.Review     => [Status.InProgress, Status.Review],
        Status.Done       => [Status.InProgress, Status.Review, Status.Done],
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    private Project ProjectWith(params object[] children)
    {
        var project = Project.Create("PMS", "Mô tả", DateTime.UtcNow.AddDays(30), _userId);

        // ProjectMapper.ToMemberResponse cần member.Employee.Name (EF Include ở prod nạp sẵn) ->
        // gán tay để giả lập, nếu không NRE khi map ToDetail().
        foreach (var member in project.Members)
            member.Employee = new Employee { Id = member.EmployeeId, Name = "PM test", Email = "pm@pms.test" };

        foreach (var child in children)
        {
            if (child is TaskItem t) project.Tasks.Add(t);
            if (child is Sprint s)   project.Sprints.Add(s);
        }
        return project;
    }

    [Fact]
    public async Task CreateAsync_tu_dong_them_nguoi_tao_lam_ProjectManager_da_Accepted()
    {
        Project? captured = null;
        // Arg.Do: chặn tham số truyền vào AddAsync để kiểm tra entity được tạo đúng chưa.
        await _projectRepo.AddAsync(Arg.Do<Project>(p => captured = p));

        await _sut.CreateAsync(new CreateProjectRequest("PMS", "Mô tả", DateTime.UtcNow.AddDays(30)));

        captured.ShouldNotBeNull();
        captured.Id.ShouldNotBe(Guid.Empty);            // BaseEntity không tự sinh Id
        var member = captured.Members.ShouldHaveSingleItem();
        member.EmployeeId.ShouldBe(_userId);
        member.RoleInProject.ShouldBe(RoleInProject.ProjectManager);
        member.InvitationStatus.ShouldBe(InvitationStatus.Accepted);

        // ADR-007: đúng 1 lần SaveChanges -> nguyên tử nhờ transaction ngầm của EF Core
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_khong_kiem_tra_quyen_tang_2()
    {
        await _sut.CreateAsync(new CreateProjectRequest("PMS", "Mô tả", DateTime.UtcNow.AddDays(30)));

        // Project chưa tồn tại nên không có RoleInProject để kiểm tra.
        // Quyền tạo là tầng 1, do policy CanCreateProject ở controller lo.
        await _authz.DidNotReceiveWithAnyArgs()
                    .AuthorizeAsync(default, default, default);
    }

    [Fact]
    public async Task GetByIdAsync_kiem_tra_quyen_View_truoc_khi_doc_du_lieu()
    {
        var project = ProjectWith();
        _authz.AuthorizeAsync(project.Id, ProjectAction.View, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.Viewer);
        _projectRepo.GetWithMembersAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        await _sut.GetByIdAsync(project.Id);

        await _authz.Received(1).AuthorizeAsync(
            project.Id, ProjectAction.View, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_yeu_cau_quyen_Update_va_trim_ten()
    {
        var project = ProjectWith();
        _authz.AuthorizeAsync(project.Id, ProjectAction.Update, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.ProjectManager);
        _projectRepo.GetWithMembersAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        byte[] clientRowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        await _sut.UpdateAsync(project.Id,
            new UpdateProjectRequest("  Tên mới  ", "Mô tả mới", DateTime.UtcNow.AddDays(60), clientRowVersion));

        project.Name.ShouldBe("Tên mới");
        // Entity đang được track -> không được gọi Update() (sẽ mark toàn bộ cột là modified)
        _projectRepo.DidNotReceive().Update(Arg.Any<Project>());
        // Optimistic concurrency chỉ hoạt động thật nếu original value bị ghi đè bằng token
        // client gửi lên, chứ không phải version vừa load trong cùng request.
        _uow.Received(1).SetConcurrencyToken(project, clientRowVersion);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_con_task_chua_Done_thi_nem_Conflict_va_khong_luu_gi()
    {
        var project = ProjectWith(TaskWithStatus(Status.InProgress));
        _authz.AuthorizeAsync(project.Id, ProjectAction.Delete, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.ProjectManager);
        _projectRepo.GetForDeletionAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var ex = await Should.ThrowAsync<ConflictException>(() => _sut.DeleteAsync(project.Id));
        ex.Message.ShouldContain("1");   // message phải nêu số task đang chặn

        // Guard chặn thì tuyệt đối không được ghi gì xuống DB
        _projectRepo.DidNotReceive().Remove(Arg.Any<Project>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_chi_con_task_Done_thi_cascade_ca_task_va_sprint()
    {
        var task = TaskWithStatus(Status.Done);
        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint 1" };
        var project = ProjectWith(task, sprint);
        _authz.AuthorizeAsync(project.Id, ProjectAction.Delete, Arg.Any<CancellationToken>())
              .Returns(RoleInProject.ProjectManager);
        _projectRepo.GetForDeletionAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        await _sut.DeleteAsync(project.Id);

        // ADR-008: cascade tường minh vì ApplySoftDelete() vô hiệu hóa cascade của EF
        _taskRepo.Received(1).Remove(task);
        _sprintRepo.Received(1).Remove(sprint);
        _projectRepo.Received(1).Remove(project);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}