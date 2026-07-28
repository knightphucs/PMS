using FluentValidation;

namespace PMS.Application.Features.Projects.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ExpectedCompletionDate)
            .GreaterThan(_ => DateTime.UtcNow.Date)
            .WithMessage("Thời gian dự kiến hoàn thành phải ở tương lai.");
    }
}