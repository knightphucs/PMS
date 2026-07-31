using FluentValidation;

namespace PMS.Application.Features.Comments.Validators;

public class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentRequestValidator()
    {
        // 2000 khớp CommentConfiguration — để 400 từ validator thay vì 500 từ SQL truncate.
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}

public class UpdateCommentRequestValidator : AbstractValidator<UpdateCommentRequest>
{
    public UpdateCommentRequestValidator()
    {
        // Sửa thành rỗng KHÔNG phải cách xóa comment — đã có DELETE riêng, và xóa qua
        // đường sửa thì không sinh được ActivityLog đúng loại.
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}
