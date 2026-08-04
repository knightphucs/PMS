using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Notifications;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Notifications;

/// <summary>
/// Job quét hạn (ADR-040). Trọng tâm là <b>khử trùng lặp</b>: đó là thứ quyết định người
/// dùng nhận một thông báo mỗi ngày hay bị dội mỗi giờ, và là thứ không lộ ra khi chạy tay
/// một lượt.
/// </summary>
public class DueDateNotifierTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly INotificationRepository _notificationRepo = Substitute.For<INotificationRepository>();

    private readonly Guid _assigneeId = Guid.NewGuid();
    private readonly Guid _watcherId = Guid.NewGuid();
    private readonly Guid _reporterId = Guid.NewGuid();

    private readonly DueDateNotifier _sut;

    public DueDateNotifierTests()
    {
        _uow.Tasks.Returns(_taskRepo);
        _uow.Notifications.Returns(_notificationRepo);

        NoPreviousNotifications();

        _sut = new DueDateNotifier(_uow, NullLogger<DueDateNotifier>.Instance);
    }

    [Fact]
    public async Task Bao_cho_ca_assignee_watcher_va_reporter()
    {
        // InterestedEmployeeIds gộp cả ba nhóm. Nếu repository quên Include Assignments /
        // Watchers thì collection rỗng và job âm thầm chỉ báo cho mỗi reporter — đúng lý do
        // GetOverdueAsync KHÔNG tái dùng được ở đây.
        HasTasks(TaskDueIn(-1));

        var created = new List<Notification>();
        _notificationRepo.When(r => r.Add(Arg.Any<Notification>()))
                         .Do(call => created.Add(call.Arg<Notification>()));

        (await _sut.RunAsync()).ShouldBe(3);

        created.Select(n => n.EmployeeId)
               .ShouldBe(new[] { _assigneeId, _watcherId, _reporterId }, ignoreOrder: true);
        created.ShouldAllBe(n => n.Type == NotificationType.DueSoon);
    }

    [Fact]
    public async Task Khong_bao_lai_nguoi_da_duoc_bao_trong_NGAY_hom_nay()
    {
        var task = TaskDueIn(-1);
        HasTasks(task);

        // Assignee đã nhận thông báo cho đúng task này hôm nay -> lượt quét sau bỏ qua họ,
        // nhưng hai người còn lại vẫn nhận. Nhờ trạng thái nằm ở DB (không phải bộ nhớ),
        // điều này đúng qua cả restart lẫn nhiều instance.
        _notificationRepo.GetNotifiedPairsSinceAsync(
                NotificationType.DueSoon, Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<(Guid, Guid)>>(_ => [(_assigneeId, task.Id)]);

        (await _sut.RunAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Chay_hai_lan_lien_tiep_khong_tao_them_thong_bao_nao()
    {
        // Chu kỳ tick là 1 giờ nhưng khóa khử trùng lặp tính theo NGÀY — đổi tick xuống 5
        // phút cũng không được làm người dùng bị dội.
        var task = TaskDueIn(-1);
        HasTasks(task);

        var sent = new List<(Guid, Guid)>();
        _notificationRepo.When(r => r.Add(Arg.Any<Notification>()))
            .Do(call =>
            {
                var n = call.Arg<Notification>();
                sent.Add((n.EmployeeId, n.RelatedEntityId!.Value));
            });
        _notificationRepo.GetNotifiedPairsSinceAsync(
                NotificationType.DueSoon, Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<(Guid, Guid)>>(_ => sent.ToList());

        (await _sut.RunAsync()).ShouldBe(3);
        (await _sut.RunAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Khong_co_task_nao_thi_khong_cham_vao_DB()
    {
        HasTasks();

        (await _sut.RunAsync()).ShouldBe(0);

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Noi_dung_phan_biet_QUA_HAN_voi_SAP_DEN_HAN()
    {
        HasTasks(TaskDueIn(-2), TaskDueIn(2));

        var created = new List<Notification>();
        _notificationRepo.When(r => r.Add(Arg.Any<Notification>()))
                         .Do(call => created.Add(call.Arg<Notification>()));

        await _sut.RunAsync();

        created.ShouldContain(n => n.Content.Contains("QUÁ HẠN"));
        created.ShouldContain(n => n.Content.Contains("sắp đến hạn"));
    }

    // ---------- helpers ----------

    private void HasTasks(params TaskItem[] tasks)
        => _taskRepo.GetDueSoonOrOverdueWithTargetsAsync(
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tasks);

    private void NoPreviousNotifications()
        => _notificationRepo.GetNotifiedPairsSinceAsync(
                Arg.Any<NotificationType>(), Arg.Any<DateTime>(),
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<(Guid, Guid)>>(_ => []);

    private TaskItem TaskDueIn(int days)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Name = "Task có hạn",
            ReporterId = _reporterId,
            DueDate = DateTime.UtcNow.Date.AddDays(days)
        };
        task.Assignments.Add(new TaskAssignment
        {
            Id = Guid.NewGuid(), TaskId = task.Id, EmployeeId = _assigneeId
        });
        task.Watchers.Add(new Watcher
        {
            TaskId = task.Id, EmployeeId = _watcherId, CreatedAt = DateTime.UtcNow
        });
        return task;
    }
}
