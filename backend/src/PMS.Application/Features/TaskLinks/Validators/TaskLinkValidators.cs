using FluentValidation;

namespace PMS.Application.Features.TaskLinks.Validators;

/// <summary>
/// 🔴 File này TỪNG KHÔNG TỒN TẠI, và hậu quả không nhìn thấy được từ ngoài:
/// <c>ValidationFilter</c> tra <c>IValidator&lt;T&gt;</c> trong DI và <b>bỏ qua im lặng</b>
/// khi không tìm thấy (<c>ValidationFilter.cs:18</c>) — không cảnh báo, không lỗi. Nên
/// <c>POST /tasks/{id}/links</c> với <c>{"linkType": 99}</c> đi thẳng qua
/// <c>TaskLinkGraph.Canonicalize</c> và <b>được lưu xuống DB</b> thành một giá trị enum
/// không tồn tại, thứ sẽ nổ ở tầng đọc của một phiên khác.
///
/// <para>
/// Cùng lớp lỗi với "ValidationFilter không bao giờ chạy cho upload" (§1): một chốt chặn
/// mà **không ai gọi tới** trông y hệt một chốt chặn đang hoạt động.
/// </para>
/// </summary>
public class CreateTaskLinkRequestValidator : AbstractValidator<CreateTaskLinkRequest>
{
    public CreateTaskLinkRequestValidator()
    {
        RuleFor(x => x.TargetTaskId)
            .NotEmpty().WithMessage("Thiếu task đích của liên kết.");

        // IsInEnum là luật QUAN TRỌNG nhất ở đây: LinkType đi thẳng vào phép chuẩn hóa
        // của ADR-038 rồi xuống DB, không có chốt nào khác phía sau.
        RuleFor(x => x.LinkType)
            .IsInEnum().WithMessage("Loại liên kết không hợp lệ.");
    }
}
