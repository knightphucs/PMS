using Microsoft.Extensions.Logging;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Notifications;

public class DueDateNotifier : IDueDateNotifier
{
    /// <summary>Báo trước bao nhiêu ngày. Task đã quá hạn cũng lọt vào vì mốc này là cận TRÊN.</summary>
    public const int HorizonDays = 3;

    private readonly IUnitOfWork _uow;
    private readonly ILogger<DueDateNotifier> _logger;

    public DueDateNotifier(IUnitOfWork uow, ILogger<DueDateNotifier> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var tasks = await _uow.Tasks.GetDueSoonOrOverdueWithTargetsAsync(HorizonDays, ct);
        if (tasks.Count == 0) return 0;

        // Tập ứng viên: mỗi (task, người quan tâm) là một thông báo tiềm năng.
        var candidates = tasks
            .SelectMany(t => t.InterestedEmployeeIds().Distinct().Select(e => (Task: t, EmployeeId: e)))
            .ToList();

        // ---- Khử trùng lặp (ADR-040) ----
        // Khóa: (EmployeeId, Type=DueSoon, RelatedEntityId=taskId, NGÀY UTC).
        // Trạng thái nằm ở DB chứ không ở bộ nhớ, nên nó đúng qua cả restart lẫn nhiều
        // instance, và độc lập với chu kỳ tick — đổi tick từ 1h sang 5 phút không làm người
        // dùng bị dội thông báo.
        var today = DateTime.UtcNow.Date;
        var taskIds = tasks.Select(t => t.Id).ToList();

        var alreadyNotified = (await _uow.Notifications.GetNotifiedPairsSinceAsync(
                NotificationType.DueSoon, today, taskIds, ct))
            .ToHashSet();

        var created = 0;
        foreach (var (task, employeeId) in candidates)
        {
            if (!alreadyNotified.Add((employeeId, task.Id))) continue;   // Add trả false = đã có

            var isOverdue = task.DueDate!.Value.Date < today;
            var content = isOverdue
                ? $"Task '{task.Name}' đã QUÁ HẠN (hạn {task.DueDate:dd/MM/yyyy})."
                : $"Task '{task.Name}' sắp đến hạn ({task.DueDate:dd/MM/yyyy}).";

            // 🔴 KHÔNG dùng INotificationService.NotifyMany: nó đọc ICurrentUserService để
            // loại người thực hiện ra khỏi danh sách nhận, mà ở đây KHÔNG CÓ HttpContext nên
            // giá trị đó là null. Việc lọc khi đó "chạy đúng" hoàn toàn do tình cờ
            // (so sánh Guid != Guid? được nâng kiểu nên luôn true). Dựng thẳng entity thì
            // hành vi là thứ đọc được từ code, không phải thứ suy ra từ một tai nạn.
            //
            // 🔴 Và TUYỆT ĐỐI không gọi IActivityLogger: Log() gọi RequireEmployeeId(), ném
            // UnauthorizedException khi không có HttpContext — job sẽ chết ở tick đầu tiên.
            _uow.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                Type = NotificationType.DueSoon,
                Content = content,
                RelatedEntityId = task.Id
            });
            created++;
        }

        if (created > 0) await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Quét hạn task: {TaskCount} task trong tầm {HorizonDays} ngày, tạo {Created} thông báo",
            tasks.Count, HorizonDays, created);

        return created;
    }
}
