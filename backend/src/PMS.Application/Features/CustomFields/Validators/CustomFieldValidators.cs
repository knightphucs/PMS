using FluentValidation;

namespace PMS.Application.Features.CustomFields.Validators;

/// <summary>
/// ⚠️ Regex màu là <b>chốt chặn thật</b>, không phải trang trí: giá trị này đi thẳng vào
/// thuộc tính <c>style</c> ở frontend. Cùng lý do đã ghi ở validator của cột board.
/// </summary>
public class FieldOptionRequestValidator : AbstractValidator<FieldOptionRequest>
{
    public FieldOptionRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Tên lựa chọn không được để trống.")
            .MaximumLength(100).WithMessage("Tên lựa chọn tối đa 100 ký tự.");

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Màu phải ở dạng #RRGGBB.");
    }
}

public class CreateFieldDefinitionRequestValidator
    : AbstractValidator<CreateFieldDefinitionRequest>
{
    public CreateFieldDefinitionRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Tên trường không được để trống.")
            .MaximumLength(100).WithMessage("Tên trường tối đa 100 ký tự.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Kiểu trường không hợp lệ.");

        // Trần 50: một ô chọn dài hơn thế thì người dùng cuộn tìm lâu hơn là gõ tay, và nó
        // là dấu hiệu trường này đáng lẽ phải là Text. Chặn ở đây rẻ hơn chặn sau khi đã có
        // dữ liệu thật.
        RuleFor(x => x.Options)
            .Must(o => o is null || o.Count <= 50)
            .WithMessage("Một trường tối đa 50 lựa chọn.");

        RuleForEach(x => x.Options).SetValidator(new FieldOptionRequestValidator());
    }
}

public class UpdateFieldDefinitionRequestValidator
    : AbstractValidator<UpdateFieldDefinitionRequest>
{
    public UpdateFieldDefinitionRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Tên trường không được để trống.")
            .MaximumLength(100).WithMessage("Tên trường tối đa 100 ký tự.");

        RuleFor(x => x.Options)
            .Must(o => o is null || o.Count <= 50)
            .WithMessage("Một trường tối đa 50 lựa chọn.");

        RuleForEach(x => x.Options).SetValidator(new FieldOptionRequestValidator());
    }
}

public class ReorderFieldDefinitionsRequestValidator
    : AbstractValidator<ReorderFieldDefinitionsRequest>
{
    public ReorderFieldDefinitionsRequestValidator()
        => RuleFor(x => x.FieldIds)
            .NotEmpty().WithMessage("Danh sách sắp xếp không được rỗng.");
}

public class SetFieldValuesRequestValidator : AbstractValidator<SetFieldValuesRequest>
{
    public SetFieldValuesRequestValidator()
    {
        RuleFor(x => x.Values).NotNull();

        // ValueText khớp giới hạn cột NVARCHAR(2000). Không kiểm ở đây thì lỗi rơi xuống
        // tầng DB thành DbUpdateException -> 500, thay vì 400 kèm thông điệp đọc được.
        RuleForEach(x => x.Values).ChildRules(v =>
            v.RuleFor(i => i.ValueText)
             .MaximumLength(2000).WithMessage("Giá trị văn bản tối đa 2000 ký tự."));
    }
}
