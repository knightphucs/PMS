namespace PMS.Domain.Enums;

/// <summary>
/// Hình dạng dữ liệu của một trường lọc được (ADR-061) — thứ quyết định toán tử nào hợp lệ
/// và giá trị người dùng gõ được phân giải ra kiểu CLR nào.
///
/// <para>
/// 🔑 <b>Đây là chỗ khoản đầu tư của ADR-059 được thu hồi.</b> Trường tuỳ biến lưu ở
/// <b>cột có kiểu</b> (<c>ValueNumber</c>/<c>ValueDate</c>/<c>ValueText</c>) chứ không phải
/// một cột JSON, nên bộ lọc so <i>số ra số, ngày ra ngày</i>. Nếu ADR-059 chọn JSON thì
/// <c>"9" &gt; "10"</c> và không index nào dùng được — ADR-059 đã ghi rõ là chọn cách lưu
/// trữ cho tính năng KẾ TIẾP, và tính năng kế tiếp đó chính là file này.
/// </para>
/// <para>
/// ⚠️ Giá trị người dùng nhập vẫn được <b>lưu dưới dạng chuỗi</b> trong
/// <see cref="Entities.SavedViewFilter.Value"/> — nó là một literal, không phải một phép so
/// sánh. Nó được <b>phân giải sang đúng kiểu lúc dựng truy vấn</b> rồi mới đem so với cột
/// có kiểu. Hai chuyện khác nhau; lẫn lộn chúng mới là cái ADR-059 chặn.
/// </para>
/// </summary>
public enum FilterValueKind
{
    /// <summary>Chuỗi tự do. Cho phép <c>Contains</c>.</summary>
    Text,

    /// <summary>Số — so sánh có thứ tự.</summary>
    Number,

    /// <summary>Mốc thời gian — so sánh có thứ tự.</summary>
    Date,

    Boolean,

    /// <summary>
    /// Khoá ngoại tới một bản ghi khác (cột board, loại việc, sprint, người, lựa chọn của
    /// trường Select). Giá trị là một <c>Guid</c>; chỉ so bằng/khác.
    /// </summary>
    Reference,

    /// <summary>
    /// Enum của hệ thống (<see cref="Priority"/>, <see cref="StatusCategory"/>). Giá trị là
    /// <b>TÊN</b> của thành viên enum chứ không phải số — số sẽ vỡ đúng theo cách ADR-052 đã
    /// trả giá khi hai enum lệch nhau. Chỉ so bằng/khác: <c>Priority.Highest = 0</c> nên
    /// "lớn hơn" đọc ngược với trực giác của người dùng, và một toán tử nói ngược nghĩa thì
    /// tệ hơn là không có.
    /// </summary>
    Enum
}
