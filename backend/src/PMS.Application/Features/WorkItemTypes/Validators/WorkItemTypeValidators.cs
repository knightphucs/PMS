using FluentValidation;

namespace PMS.Application.Features.WorkItemTypes.Validators;

/// <summary>
/// ⚠️ Regex màu là chốt chặn thật — giá trị đi thẳng vào thuộc tính <c>style</c> ở frontend.
/// <c>Icon</c> chỉ nhận chữ cái vì nó được tra trong bảng icon của <c>lucide-react</c>:
/// nhận chuỗi tuỳ ý là mời một payload đi vào chỗ frontend dùng để chọn component.
/// </summary>
public class CreateWorkItemTypeRequestValidator : AbstractValidator<CreateWorkItemTypeRequest>
{
    public CreateWorkItemTypeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên loại không được để trống.")
            .MaximumLength(50).WithMessage("Tên loại tối đa 50 ký tự.");

        RuleFor(x => x.Icon)
            .Matches("^[A-Za-z]{1,50}$").WithMessage("Icon chỉ gồm chữ cái, tối đa 50 ký tự.");

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Màu phải ở dạng #RRGGBB.");
    }
}

public class UpdateWorkItemTypeRequestValidator : AbstractValidator<UpdateWorkItemTypeRequest>
{
    public UpdateWorkItemTypeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên loại không được để trống.")
            .MaximumLength(50).WithMessage("Tên loại tối đa 50 ký tự.");

        RuleFor(x => x.Icon)
            .Matches("^[A-Za-z]{1,50}$").WithMessage("Icon chỉ gồm chữ cái, tối đa 50 ký tự.");

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Màu phải ở dạng #RRGGBB.");
    }
}

public class ReorderWorkItemTypesRequestValidator : AbstractValidator<ReorderWorkItemTypesRequest>
{
    public ReorderWorkItemTypesRequestValidator()
        => RuleFor(x => x.TypeIds)
            .NotEmpty().WithMessage("Danh sách sắp xếp không được rỗng.");
}
