using FluentValidation;

namespace PMS.Application.Features.Approvals.Validators;

/// <summary>
/// ⚠️ Ở đây chỉ có phép kiểm <b>hình dạng</b> (rỗng / khoảng giá trị / enum hợp lệ). Luật thật
/// sự — "loại việc và cột có thuộc project này không", "người duyệt có phải thành viên đang
/// hoạt động không", "quorum có với tới được không" — nằm ở <see cref="ApprovalService"/>, vì
/// cả ba đều cần dữ liệu mà validator không có.
/// </summary>
public class CreateApprovalPolicyRequestValidator : AbstractValidator<CreateApprovalPolicyRequest>
{
    public CreateApprovalPolicyRequestValidator()
    {
        RuleFor(x => x.WorkItemTypeId)
            .NotEmpty().WithMessage("Phải chọn loại công việc mà luật này áp lên.");

        RuleFor(x => x.TargetColumnId)
            .NotEmpty().WithMessage("Phải chọn cột đích cần được duyệt.");

        RuleFor(x => x.ApproverMode)
            .IsInEnum().WithMessage("Chế độ người duyệt không hợp lệ.");

        // Chặn trên ở 20: quorum lớn hơn thế là dấu hiệu nhập nhầm chứ không phải một quy
        // trình thật. CHECK constraint ở DB giữ cận dưới (>= 1) như một chốt chặn thứ hai.
        RuleFor(x => x.MinApprovals)
            .InclusiveBetween(1, 20)
            .WithMessage("Số lượt duyệt cần có phải nằm trong khoảng 1–20.");
    }
}

public class UpdateApprovalPolicyRequestValidator : AbstractValidator<UpdateApprovalPolicyRequest>
{
    public UpdateApprovalPolicyRequestValidator()
    {
        RuleFor(x => x.WorkItemTypeId)
            .NotEmpty().WithMessage("Phải chọn loại công việc mà luật này áp lên.");

        RuleFor(x => x.TargetColumnId)
            .NotEmpty().WithMessage("Phải chọn cột đích cần được duyệt.");

        RuleFor(x => x.ApproverMode)
            .IsInEnum().WithMessage("Chế độ người duyệt không hợp lệ.");

        RuleFor(x => x.MinApprovals)
            .InclusiveBetween(1, 20)
            .WithMessage("Số lượt duyệt cần có phải nằm trong khoảng 1–20.");
    }
}

public class CreateApprovalDecisionRequestValidator : AbstractValidator<CreateApprovalDecisionRequest>
{
    public CreateApprovalDecisionRequestValidator()
    {
        RuleFor(x => x.Decision)
            .IsInEnum().WithMessage("Quyết định không hợp lệ.");

        // Không NotEmpty kể cả khi từ chối: cưỡng chế một ô chữ không làm lý do trở nên thật,
        // nó chỉ sinh ra những dòng "n/a". Frontend nhắc, backend không chặn.
        RuleFor(x => x.Comment)
            .MaximumLength(1000).WithMessage("Lý do tối đa 1000 ký tự.");
    }
}
