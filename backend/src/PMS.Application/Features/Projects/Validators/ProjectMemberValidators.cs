using FluentValidation;

namespace PMS.Application.Features.Projects.Validators;

public class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(256);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Vai trò không hợp lệ.");
    }
}

public class InviteExternalRequestValidator : AbstractValidator<InviteExternalRequest>
{
    public InviteExternalRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(256);

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Vai trò không hợp lệ.");
    }
}
