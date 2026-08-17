using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Một góc nhìn đã lưu trên danh sách task của MỘT project (ADR-061) — bộ lọc + sắp xếp +
/// gom nhóm + tập cột hiển thị.
///
/// <para>
/// Đây là mảnh cuối của "nền tảng mở rộng": ADR-052 cho đội tự khai <b>cột</b>, ADR-059 cho
/// tự khai <b>trường</b>, ADR-060 cho tự khai <b>loại việc</b> — còn đây cho họ tự dựng
/// <b>góc nhìn</b> thay vì nhận một bố cục cố định.
/// </para>
/// <para>
/// 🔑 <b>Và nó là tiền đề thật của tầng "bộ máy quy trình" (§0).</b> Một "hàng đợi" —
/// <i>"mọi Change Request đang chờ tôi duyệt"</i> — chính là một view lưu được, không phải
/// một màn hình mới. Làm quy trình trước sẽ phải dựng một màn danh sách tạm rồi vứt đi.
/// </para>
/// </summary>
public class SavedView : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>Tên do người dùng đặt — "Việc quá hạn của tôi", "CR chờ duyệt"…</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Người tạo. Giữ lại cả khi view đã <see cref="IsShared"/>: nó là thông tin quy trách
    /// nhiệm ("ai dựng cái này"), không chỉ là khoá phân quyền.
    /// </summary>
    public Guid OwnerId { get; set; }
    public Employee Owner { get; set; } = null!;

    /// <summary>
    /// <c>false</c> = chỉ chủ sở hữu thấy; <c>true</c> = mọi thành viên project thấy.
    ///
    /// <para>
    /// ⚠️ Quyền GHI lên một view đã chia sẻ là <b>chủ sở hữu HOẶC người có
    /// <c>ManageSavedViews</c></b> (PM), không phải mọi thành viên. Chỉ-chủ-sở-hữu sẽ khoá
    /// chết view chung của cả đội khi người đó rời dự án; mọi-thành-viên thì ai cũng ghi đè
    /// công của nhau. Xem ADR-061.
    /// </para>
    /// </summary>
    public bool IsShared { get; set; }

    /// <summary>Thứ tự hiển thị trên thanh chọn view. Không unique — xem <see cref="BoardColumn.Order"/>.</summary>
    public int Order { get; set; }

    // ---------- sắp xếp ----------

    /// <summary>
    /// Trường dựng sẵn dùng để sắp xếp. Rỗng = mặc định (hạn rồi tie-break bằng Id).
    ///
    /// <para>
    /// 📌 <b>Cố ý CHỈ trường dựng sẵn — sắp theo trường tuỳ biến chưa ship, và lý do là
    /// cascade chứ không phải độ khó.</b> Một khoá ngoại <c>SortByFieldDefinitionId</c> trên
    /// chính hàng này buộc phải là <c>SET NULL</c> (xoá trường thì view mất kiểu sắp xếp chứ
    /// không được biến mất theo). Nhưng khi đó <c>FieldDefinitions</c> chạm tới
    /// <see cref="SavedViewFilter"/> bằng <b>hai</b> lối —
    /// <c>FieldDefinitions →SET NULL→ SavedViews →CASCADE→ SavedViewFilters</c> và
    /// <c>FieldDefinitions →CASCADE→ SavedViewFilters</c> — mà phép kiểm của SQL Server là
    /// <b>tĩnh</b>, không quan tâm lối thứ hai có bao giờ chạy hay không. Đó đúng là hình
    /// dạng đã làm 18 test đỏ trong 12ms ở ADR-059.
    /// </para>
    /// <para>
    /// Lối thoát nếu về sau cần: một bảng <c>SavedViewSorts</c> riêng cùng khuôn
    /// <see cref="SavedViewFilter"/> (khi đó cả hai lối đều là CASCADE vào một bảng con
    /// không nằm dưới bảng kia — an toàn, đúng phân tích của ADR-060). Chưa làm vì nó là một
    /// bảng cho tối đa một hàng. <b>Lọc</b> theo trường tuỳ biến — phần thật sự đáng giá, và
    /// là thứ cột-có-kiểu của ADR-059 được mua về để phục vụ — thì hoạt động đầy đủ.
    /// </para>
    /// </summary>
    public TaskField? SortBy { get; set; }

    public bool SortDescending { get; set; }

    // ---------- gom nhóm ----------

    /// <summary>
    /// Gom nhóm — <b>chỉ trường dựng sẵn</b>, cố ý.
    ///
    /// <para>
    /// 📌 Gom theo một trường <c>MultiSelect</c> thì một task thuộc NHIỀU nhóm cùng lúc, và
    /// khi đó tổng số dòng hiển thị lớn hơn số task — một màn hình tự mâu thuẫn mà người
    /// dùng không có cách nào hiểu. Chưa có câu trả lời tốt nên chưa ship (§0, nguyên tắc
    /// "không ship một cờ không chặn được gì").
    /// </para>
    /// </summary>
    public TaskField? GroupBy { get; set; }

    // ---------- thành phần ----------

    public ICollection<SavedViewFilter> Filters { get; set; } = new List<SavedViewFilter>();

    /// <summary>
    /// Tập cột hiển thị, theo thứ tự. Rỗng = dùng bộ cột mặc định của màn danh sách — cố ý
    /// KHÔNG nhân bản danh sách mặc định vào từng view: làm vậy thì đổi bộ mặc định về sau
    /// sẽ không chạm tới view nào đã tạo, và không ai hiểu vì sao.
    /// </summary>
    public ICollection<SavedViewColumn> Columns { get; set; } = new List<SavedViewColumn>();
}
