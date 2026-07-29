using FluentValidation;

namespace PMS.Application.Features.Projects.Validators;

public class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
        => RuleFor(x => x.Role).IsInEnum().WithMessage("Vai trò không hợp lệ.");
}