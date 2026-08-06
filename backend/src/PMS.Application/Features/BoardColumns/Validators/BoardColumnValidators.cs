using FluentValidation;

namespace PMS.Application.Features.BoardColumns.Validators;

/// <summary>
/// ⚠️ Regex màu là <b>chốt chặn thật</b>, không phải trang trí: giá trị này đi thẳng vào
/// thuộc tính <c>style</c> ở frontend. Nhận chuỗi tuỳ ý là mở một đường chèn CSS.
/// </summary>
public class CreateBoardColumnRequestValidator : AbstractValidator<CreateBoardColumnRequest>
{
    public CreateBoardColumnRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên cột không được để trống.")
            .MaximumLength(50).WithMessage("Tên cột tối đa 50 ký tự.");

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$")
            .WithMessage("Màu phải ở dạng #RRGGBB.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Nhóm trạng thái không hợp lệ.");
    }
}

public class UpdateBoardColumnRequestValidator : AbstractValidator<UpdateBoardColumnRequest>
{
    public UpdateBoardColumnRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên cột không được để trống.")
            .MaximumLength(50).WithMessage("Tên cột tối đa 50 ký tự.");

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$")
            .WithMessage("Màu phải ở dạng #RRGGBB.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Nhóm trạng thái không hợp lệ.");
    }
}

public class ReorderBoardColumnsRequestValidator : AbstractValidator<ReorderBoardColumnsRequest>
{
    public ReorderBoardColumnsRequestValidator()
        // "Đủ và đúng tập cột hiện có" cần đọc DB nên nằm ở Service; ở đây chỉ chặn
        // danh sách rỗng, thứ chắc chắn sai mà không cần biết gì về dữ liệu.
        => RuleFor(x => x.OrderedColumnIds)
            .NotEmpty().WithMessage("Danh sách sắp xếp không được rỗng.");
}
