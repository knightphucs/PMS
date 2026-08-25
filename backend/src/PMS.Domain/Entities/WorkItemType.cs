using PMS.Domain.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// Loại công việc của MỘT project (ADR-060) — "Sự cố", "Yêu cầu", "Change Request",
/// "Bảo trì"… thay cho việc mọi thứ đều là một "Task" duy nhất.
///
/// <para>
/// Đây là điểm rẽ dứt khoát khỏi mô hình mini-Jira: hệ thống <b>không hardcode một loại
/// nào</b>. Đội hạ tầng tự khai loại của họ, và mỗi loại lộ ra một tập trường tuỳ biến khác
/// nhau (ADR-059) qua <see cref="Fields"/>.
/// </para>
/// <para>
/// 🔴 Mọi task BẮT BUỘC thuộc đúng một loại — cùng bất biến với cột board (ADR-052). Vì vậy
/// project mới được cấp sẵn một loại mặc định, và xoá loại thì phải chọn loại đích cho task
/// đang mang nó.
/// </para>
/// </summary>
public class WorkItemType : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tên icon của <c>lucide-react</c> (ví dụ <c>"CircleAlert"</c>). Lưu TÊN chứ không lưu
    /// SVG: SVG do người dùng nhập là một đường chèn script, còn một tên nằm ngoài danh sách
    /// frontend biết thì chỉ rơi về icon mặc định — hỏng nhẹ và nhìn thấy được.
    /// </summary>
    public string Icon { get; set; } = "CircleDot";

    /// <summary>Mã màu <c>#RRGGBB</c> cho chip loại.</summary>
    public string Color { get; set; } = "#6B7280";

    public int Order { get; set; }

    /// <summary>
    /// Loại này có nhận yêu cầu từ <b>người ngoài project</b> qua cổng tiếp nhận không
    /// (ADR-063). Mặc định <c>false</c> — mọi loại đã có từ trước ADR-063 giữ nguyên hành vi.
    ///
    /// <para>
    /// 🔑 <b>Không có khái niệm "request type" riêng, và đó là toàn bộ điểm của ADR-063.</b>
    /// <see cref="WorkItemType"/> + <see cref="Fields"/> + <c>IsRequired</c> (ADR-060) đã là
    /// một request type đầy đủ; dựng một khái niệm song song sẽ là hai thứ cùng nghĩa phải
    /// giữ đồng bộ mãi mãi. Cờ này chỉ trả lời <i>"ai được tạo"</i>, không đổi <i>"nó là
    /// cái gì"</i>.
    /// </para>
    /// <para>
    /// 🔴 Cờ này CHẶN THẬT (luật 4 Doctrine §0): <c>POST /request-portal/.../requests</c>
    /// từ chối mọi loại có <c>IsRequestable == false</c>, và có mutation test canh. Một cờ
    /// không chặn được gì là một trường chết đội lốt tính năng — tiền lệ
    /// <c>Project.Status</c> (ADR-048).
    /// </para>
    /// </summary>
    public bool IsRequestable { get; set; }

    /// <summary>
    /// Chỉ dẫn hiện trên đầu form tiếp nhận — "kèm mã tài sản nếu có", "yêu cầu khẩn thì
    /// gọi trực ban trước". Null = không hiện khối chỉ dẫn nào (luật 3 Doctrine: tính năng
    /// chưa dùng phải TỰ ẨN, không hiện rỗng).
    ///
    /// <para>
    /// ⚠️ Chỉ có nghĩa khi <see cref="IsRequestable"/> bật. Không cưỡng chế quan hệ đó ở
    /// tầng dữ liệu: tắt cờ rồi bật lại mà mất chữ đã gõ thì tệ hơn hẳn một cột thừa nghĩa.
    /// </para>
    /// </summary>
    public string? RequestInstructions { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    /// <summary>Các trường tuỳ biến mà loại này dùng, kèm cờ bắt buộc và thứ tự.</summary>
    public ICollection<WorkItemTypeField> Fields { get; set; } = new List<WorkItemTypeField>();

    /// <summary>
    /// Loại mặc định cấp cho project mới — giữ nguyên hành vi trước ADR-060 (mọi thứ là
    /// "Task"), nên project không đụng gì tới cấu hình loại vẫn hành xử y như cũ.
    /// </summary>
    /// <remarks>
    /// Dùng chung cho <c>Project.Create</c>, <c>DbSeeder</c> và backfill của migration. Ba
    /// nơi tự định nghĩa danh sách này thì dữ liệu cũ và mới lệch nhau ngay từ ngày đầu —
    /// đúng bài học đã ghi ở <see cref="BoardColumn.CreateDefaults"/>.
    /// </remarks>
    public static WorkItemType CreateDefault(Guid projectId) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        Name = "Task",
        Icon = "CircleDot",
        Color = "#6B7280",
        Order = 0,
    };
}
