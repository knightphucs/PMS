using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class Sprint : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    /// <summary>Vòng đời do NGƯỜI DÙNG điều khiển — xem <see cref="SprintStatus"/>.</summary>
    public SprintStatus Status { get; private set; } = SprintStatus.Planned;

    /// <summary>Mốc đóng sổ thật, dùng cho velocity. <c>null</c> khi chưa đóng.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Bắt đầu sprint. Chỉ đi được từ <see cref="SprintStatus.Planned"/>.
    /// </summary>
    /// <remarks>
    /// KHÔNG tự dời <c>StartDate</c> về hôm nay: ngày là kế hoạch đã cam kết, còn việc bấm
    /// nút sớm hay muộn vài hôm là chuyện vận hành. Ghi đè kế hoạch bằng thực tế sẽ xóa mất
    /// đúng thứ mà báo cáo cần để so sánh hai bên.
    /// </remarks>
    public void Start()
    {
        if (Status == SprintStatus.Active) return;   // idempotent, không phải lỗi

        if (Status == SprintStatus.Completed)
            throw new DomainException("Sprint đã đóng thì không bắt đầu lại được.");

        Status = SprintStatus.Active;
    }

    /// <summary>
    /// Đóng sprint. Chỉ đi được từ <see cref="SprintStatus.Active"/>.
    ///
    /// <para>
    /// ⚠️ Method này <b>không</b> quyết định task chưa xong đi đâu — đó là lựa chọn của
    /// người đóng và được truyền xuống từ tầng Service (ADR-050). Domain chỉ giữ bất biến
    /// của chính sprint.
    /// </para>
    /// </summary>
    public void Complete(DateTime completedAt)
    {
        if (Status == SprintStatus.Completed)
            throw new DomainException("Sprint này đã được đóng rồi.");

        if (Status == SprintStatus.Planned)
            throw new DomainException("Sprint chưa bắt đầu thì chưa có gì để đóng.");

        Status = SprintStatus.Completed;
        CompletedAt = completedAt;
    }

    // Property computed (không lưu cứng), cùng lý do với TaskItem.IsOverdue:
    // Mapperly chỉ map được property, không map method.
    public bool IsActive
    {
        get
        {
            var today = DateTime.UtcNow.Date;
            return today >= StartDate.Date && today <= EndDate.Date;
        }
    }

    public void AddTask(TaskItem task)
    {
        task.SprintId = Id;
        Tasks.Add(task);
    }

    public void RemoveTask(TaskItem task)
    {
        task.SprintId = null;
        Tasks.Remove(task);
    }
}
