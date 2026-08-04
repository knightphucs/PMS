using FluentValidation;

namespace PMS.Application.Features.Tasks.Validators;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);   // khớp TaskItemConfiguration
        RuleFor(x => x.Description).MaximumLength(4000);      // nullable -> không NotEmpty
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();

        // DueDate cho phép ở quá khứ: task quá hạn là trạng thái hợp lệ và cần hiển thị
        // được (IsOverdue), khác với ExpectedCompletionDate của Project.
    }
}

public class AssignTaskRequestValidator : AbstractValidator<AssignTaskRequest>
{
    public AssignTaskRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("Thiếu RowVersion — client phải gửi lại giá trị đã nhận từ lần GET gần nhất.");
    }
}
