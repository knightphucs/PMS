using FluentValidation;

namespace PMS.Application.Features.Labels.Validators;

/// <summary>Regex màu dùng chung — một chỗ định nghĩa để hai validator không lệch nhau.</summary>
internal static class LabelRules
{
    public const string HexColorPattern = "^#[0-9A-Fa-f]{6}$";
    public const string HexColorMessage = "Màu phải ở dạng #RRGGBB, ví dụ #2563EB.";
}

public class CreateLabelRequestValidator : AbstractValidator<CreateLabelRequest>
{
    public CreateLabelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);   // khớp LabelConfiguration

        // Color nullable ở request: bỏ trống thì service dùng Label.DefaultColor.
        RuleFor(x => x.Color)
            .Matches(LabelRules.HexColorPattern).WithMessage(LabelRules.HexColorMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.Color));
    }
}

public class UpdateLabelRequestValidator : AbstractValidator<UpdateLabelRequest>
{
    public UpdateLabelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color)
            .NotEmpty()
            .Matches(LabelRules.HexColorPattern).WithMessage(LabelRules.HexColorMessage);
    }
}
