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

/// <summary>
/// 🔴 Thiếu validator này cho tới 2026-08-04. Vì <c>ValidationFilter</c> bỏ qua im lặng khi
/// không có <c>IValidator&lt;T&gt;</c>, <c>PATCH /tasks/{id}/status</c> với
/// <c>{"target": 99}</c> đi thẳng tới <c>task.ChangeStatus(request.Target)</c> mang một giá
/// trị enum không tồn tại — state machine so sánh nó với mọi nhánh hợp lệ, không khớp cái
/// nào, và ném ra lỗi nghiệp vụ khó hiểu thay vì một 400 nói rõ đầu vào sai.
/// </summary>
public class ChangeTaskStatusRequestValidator : AbstractValidator<ChangeTaskStatusRequest>
{
    public ChangeTaskStatusRequestValidator()
        // ADR-052: cột đích là dữ liệu của project, không còn là enum nên `IsInEnum`
        // không còn nghĩa. Validator chỉ chặn được Guid rỗng; "cột này có thuộc project của
        // task không" là câu hỏi cần DB nên nằm ở Service (trả 404).
        => RuleFor(x => x.TargetColumnId)
            .NotEmpty().WithMessage("Phải chọn cột đích.");
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
