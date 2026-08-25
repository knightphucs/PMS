using PMS.Application.Features.CustomFields;
using PMS.Domain.Enums;

namespace PMS.Application.Features.RequestPortal;

// ---------- (1) danh mục cổng ----------

/// <summary>
/// Một project đang mở cổng tiếp nhận, nhìn từ phía NGƯỜI NGOÀI.
///
/// <para>
/// 🔴 <b>Guard G4 sống trong hình dạng của record này.</b> Người nhận nó thường không phải
/// thành viên project, nên mọi trường thêm vào đây là một rò rỉ tiềm năng. Cụ thể những
/// thứ <b>cố ý vắng mặt</b>: danh sách thành viên · cột board · sprint · số lượng task ·
/// mô tả project · trạng thái project. Không cái nào cần để điền một cái form.
/// </para>
/// <para>
/// ⚠️ Có test khẳng định trên <b>JSON thô</b> chứ không deserialize vào record này —
/// deserialize sẽ âm thầm bỏ qua trường thừa và test vẫn xanh (tiền lệ
/// <c>GET /employees?search=</c>, ADR-048).
/// </para>
/// </summary>
public record RequestPortalProjectResponse(
    Guid ProjectId,
    string ProjectName,
    string ProjectKey,
    IReadOnlyList<RequestPortalTypeSummary> Types);

/// <summary>Một loại yêu cầu gửi được, đủ để vẽ một thẻ chọn.</summary>
public record RequestPortalTypeSummary(
    Guid WorkItemTypeId,
    string Name,
    string Icon,
    string Color);

// ---------- (2) lược đồ form ----------

/// <summary>
/// Form tiếp nhận của MỘT project — dựng thẳng từ <c>WorkItemTypeFields</c> (ADR-060).
/// Không có bảng "form" nào cả; đó là toàn bộ điểm của ADR-063.
/// </summary>
public record RequestPortalFormResponse(
    Guid ProjectId,
    string ProjectName,
    string ProjectKey,
    IReadOnlyList<RequestPortalTypeForm> Types);

public record RequestPortalTypeForm(
    Guid WorkItemTypeId,
    string Name,
    string Icon,
    string Color,
    /// <summary>Chỉ dẫn của loại này; <c>null</c> = không hiện khối chỉ dẫn (luật 3 Doctrine §0).</summary>
    string? RequestInstructions,
    IReadOnlyList<RequestPortalFieldSchema> Fields);

/// <summary>
/// Một ô trên form. Tái dùng <see cref="FieldOptionResponse"/> của ADR-059 nguyên vẹn —
/// frontend đã có bộ render theo <see cref="FieldType"/>, không dựng lại.
/// </summary>
public record RequestPortalFieldSchema(
    Guid FieldDefinitionId,
    string Label,
    FieldType Type,
    bool IsRequired,
    int Order,
    IReadOnlyList<FieldOptionResponse> Options);

// ---------- (3) gửi yêu cầu ----------

/// <summary>
/// ⚠️ <b>KHÔNG</b> có <c>BoardColumnId</c>, <c>SprintId</c>, <c>ParentTaskId</c>,
/// <c>StoryPoints</c>, <c>AssigneeIds</c>. Người gửi yêu cầu không biết — và không được
/// quyết — task của họ rơi vào cột nào hay ai xử lý. Nhận những trường đó rồi bỏ qua im
/// lặng thì tệ hơn: nó hứa một quyền điều khiển không tồn tại.
/// </summary>
public record SubmitRequestRequest(
    Guid WorkItemTypeId,
    string Name,
    string? Description,
    Priority Priority,
    DateTime? DueDate,
    IReadOnlyList<SetFieldValueRequest>? FieldValues = null);

// ---------- (4)(5) yêu cầu của tôi ----------

/// <summary>
/// Một yêu cầu trong danh sách "Yêu cầu của tôi".
///
/// <para>
/// 📌 <see cref="StatusName"/> là TÊN CỘT do đội xử lý đặt ("Tiếp nhận", "Đang xử lý"),
/// không phải một enum. Người gửi vì vậy đọc được đúng ngôn ngữ quy trình của đội đó —
/// và đó là lý do ADR-052 bỏ enum trạng thái ngay từ đầu.
/// </para>
/// </summary>
public record MyRequestResponse(
    Guid TaskId,
    string Code,
    string Name,
    Guid ProjectId,
    string ProjectName,
    string TypeName,
    string TypeIcon,
    string TypeColor,
    string StatusName,
    string StatusColor,
    StatusCategory StatusCategory,
    Priority Priority,
    DateTime? DueDate,
    DateTime CreatedAt,
    TaskApprovalState ApprovalState);

/// <summary>
/// Chi tiết một yêu cầu của CHÍNH người gửi — chỉ đọc.
///
/// <para>
/// ⚠️ <b>Cố ý KHÔNG có bình luận</b>, và điều đó đã ghi rõ ở ADR-063 chứ không phải bỏ
/// quên: <c>CommentService</c> đi qua <c>ProjectAction.CreateComment</c> nên người ngoài
/// nhận 404. Mở đường hồi đáp cần một quyết định sản phẩm riêng (ai đọc được bình luận
/// nội bộ của đội xử lý?), không phải một chi tiết cài đặt.
/// </para>
/// </summary>
public record MyRequestDetailResponse(
    Guid TaskId,
    string Code,
    string Name,
    string? Description,
    Guid ProjectId,
    string ProjectName,
    string TypeName,
    string TypeIcon,
    string TypeColor,
    string StatusName,
    string StatusColor,
    StatusCategory StatusCategory,
    Priority Priority,
    DateTime? DueDate,
    DateTime CreatedAt,
    TaskApprovalState ApprovalState,
    IReadOnlyList<FieldValueResponse> FieldValues);
