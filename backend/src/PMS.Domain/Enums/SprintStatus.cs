namespace PMS.Domain.Enums;

/// <summary>
/// Vòng đời của một sprint (ADR-050).
///
/// <para>
/// 🔴 <b>Khác hẳn <c>Sprint.IsActive</c>.</b> Thuộc tính đó suy từ NGÀY (hôm nay có nằm
/// giữa StartDate và EndDate không) và vẫn giữ nguyên; nó trả lời "sprint này có đang trong
/// khoảng thời gian của nó không". Còn enum này trả lời một câu khác: <b>đội đã bắt đầu
/// chưa, và đã chốt sổ chưa</b> — thứ mà ngày tháng không biết.
/// </para>
/// <para>
/// Hai câu hỏi đó tách nhau trong thực tế: một sprint đã qua ngày kết thúc mà chưa ai đóng
/// vẫn là sprint đang chạy dở, và velocity chỉ đếm được sprint đã <see cref="Completed"/>.
/// </para>
/// </summary>
public enum SprintStatus
{
    /// <summary>Đã lập kế hoạch, chưa bắt đầu. Trạng thái của mọi sprint mới tạo.</summary>
    Planned,

    /// <summary>Đang chạy. Một project chỉ có tối đa MỘT sprint ở trạng thái này.</summary>
    Active,

    /// <summary>
    /// Đã đóng sổ. Mốc này là thứ nhóm báo cáo dùng để đo velocity — không có nó thì không
    /// có gì để tính tốc độ theo.
    /// </summary>
    Completed,
}
