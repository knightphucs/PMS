namespace PMS.Application.Features.Approvals;

/// <summary>
/// Mã lỗi máy đọc được của cổng duyệt (ADR-062), trả về trong <c>code</c> của ProblemDetails.
///
/// <para>
/// 🔴 Ba nhánh dưới đây đều là <b>409</b>, nhưng client phải xử lý khác nhau — nên chúng cần
/// một tín hiệu không phải là câu chữ. Khớp theo thông điệp tiếng Việt sẽ là hai nơi cùng
/// dựng một luật, và nó sẽ hỏng im lặng ở lần đầu tiên có ai sửa lại câu văn (ADR-034).
/// </para>
/// <para>
/// Danh mục ĐÓNG, và cố ý nằm ở tầng Application chứ không ở Domain: đây là hợp đồng với
/// client, không phải một khái niệm nghiệp vụ.
/// </para>
/// </summary>
public static class ApprovalCodes
{
    /// <summary>
    /// Vừa gửi yêu cầu duyệt giúp người dùng.
    ///
    /// <para>
    /// ⚠️ Đây là 409 <b>duy nhất</b> trong hệ thống mà thao tác của người dùng đã thành công
    /// một nửa: task không di chuyển, nhưng một yêu cầu duyệt đã được tạo và những người ký
    /// đã được báo. Client phải hiện nó như một <b>thông báo trung tính</b> — một toast đỏ
    /// nói dối về thứ vừa xảy ra, và người dùng sẽ bấm lại lần nữa vì tưởng chưa có gì.
    /// </para>
    /// </summary>
    public const string Requested = "approval_requested";

    /// <summary>Đã có yêu cầu đang chờ — không sinh thêm. Lỗi thật, nhưng nhẹ.</summary>
    public const string Pending = "approval_pending";

    /// <summary>Đã bị từ chối và vẫn đang chặn. Lối thoát là huỷ yêu cầu rồi gửi lại.</summary>
    public const string Rejected = "approval_rejected";
}
