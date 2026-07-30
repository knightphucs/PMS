using FluentValidation;

namespace PMS.Application.Features.Admin.Validators;

public class LockAccountRequestValidator : AbstractValidator<LockAccountRequest>
{
    public LockAccountRequestValidator()
        => RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải nêu lý do khóa tài khoản.")
            .MaximumLength(256);
}

public class ChangeSystemRoleRequestValidator : AbstractValidator<ChangeSystemRoleRequest>
{
    public ChangeSystemRoleRequestValidator()
        => RuleFor(x => x.Role).IsInEnum().WithMessage("Vai trò hệ thống không hợp lệ.");
}