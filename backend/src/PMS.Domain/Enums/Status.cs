namespace PMS.Domain.Enums;

/// <summary>
/// Trạng thái của <b>PROJECT</b> — bốn giá trị cố định, không tuỳ biến được.
///
/// <para>
/// 🔴 <b>Trước ADR-052, enum này DÙNG CHUNG cho cả Project lẫn TaskItem.</b> Khi task
/// chuyển sang cột tuỳ biến theo từng project (<see cref="PMS.Domain.Entities.BoardColumn"/>),
/// việc dùng chung trở thành một cái bẫy: mọi thay đổi phục vụ vòng đời task sẽ kéo theo
/// trạng thái project, trong khi project chỉ cần đúng bốn nấc và không ai đòi thêm cột cho nó.
/// </para>
/// <para>
/// ⚠️ Giữ nguyên <b>tên và thứ tự</b> thành viên là bắt buộc: EF lưu enum dưới dạng
/// <c>int</c>, nên cột <c>Projects.Status</c> trong DB vẫn là những số cũ. Đổi thứ tự ở đây
/// là âm thầm đổi trạng thái của mọi project đang có, và không migration nào bắt được.
/// </para>
/// </summary>
public enum Status
{
    ToDo,
    InProgress,
    Review,
    Done,
}
