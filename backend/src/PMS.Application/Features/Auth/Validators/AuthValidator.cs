using FluentValidation;

namespace PMS.Application.Features.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Xác nhận mật khẩu không khớp.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số.");
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        // CHỈ kiểm dạng email. Không có luật nào chạm tới việc email đó CÓ TỒN TẠI hay
        // không — một rule async kiểu "email phải tồn tại" sẽ trả 400 cho email lạ và làm
        // hỏng đúng thứ ADR-041 bảo vệ (endpoint phải im lặng như nhau ở cả hai nhánh).
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();

        // Cùng bộ luật với RegisterRequest — mật khẩu đặt lại không được yếu hơn mật khẩu
        // đăng ký, nếu không luồng "quên mật khẩu" thành đường vòng để né chính sách.
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu không khớp.");
    }
}
