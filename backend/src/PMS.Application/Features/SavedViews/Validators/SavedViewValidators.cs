using FluentValidation;

namespace PMS.Application.Features.SavedViews.Validators;

/// <summary>
/// ⚠️ Ở đây chỉ có phép kiểm <b>hình dạng</b> (rỗng/độ dài/enum hợp lệ). Luật thật sự —
/// "đúng một nguồn trường", "toán tử này dùng được với kiểu này không", "literal có parse
/// được không", "trường tuỳ biến có thuộc project này không" — nằm ở
/// <c>TaskFilterCatalog</c> và <c>SavedViewService</c>, vì cả bốn đều cần dữ liệu mà
/// validator không có (lược đồ trường của project).
/// </summary>
public class SavedViewFilterDtoValidator : AbstractValidator<SavedViewFilterDto>
{
    public SavedViewFilterDtoValidator()
    {
        RuleFor(x => x.Operator)
            .IsInEnum().WithMessage("Toán tử lọc không hợp lệ.");

        RuleFor(x => x.Field)
            .IsInEnum().When(x => x.Field.HasValue)
            .WithMessage("Trường lọc không hợp lệ.");

        RuleFor(x => x.Value)
            .MaximumLength(200).WithMessage("Giá trị lọc tối đa 200 ký tự.");
    }
}

public class SavedViewColumnDtoValidator : AbstractValidator<SavedViewColumnDto>
{
    public SavedViewColumnDtoValidator()
        => RuleFor(x => x.Field)
            .IsInEnum().When(x => x.Field.HasValue)
            .WithMessage("Cột hiển thị không hợp lệ.");
}

public class CreateSavedViewRequestValidator : AbstractValidator<CreateSavedViewRequest>
{
    public CreateSavedViewRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên view không được để trống.")
            .MaximumLength(100).WithMessage("Tên view tối đa 100 ký tự.");

        RuleFor(x => x.SortBy).IsInEnum().When(x => x.SortBy.HasValue);
        RuleFor(x => x.GroupBy).IsInEnum().When(x => x.GroupBy.HasValue);

        // Trần 20 điều kiện: nhiều hơn thế thì mỗi điều kiện là một subquery trên FieldValues
        // và câu SQL phình rất nhanh. Nó cũng là dấu hiệu người dùng cần một view khác chứ
        // không phải thêm điều kiện. Chặn ở đây rẻ hơn chặn sau khi đã có dữ liệu thật.
        RuleFor(x => x.Filters)
            .Must(f => f is null || f.Count <= 20)
            .WithMessage("Một view tối đa 20 điều kiện lọc.");

        RuleFor(x => x.Columns)
            .Must(c => c is null || c.Count <= 30)
            .WithMessage("Một view tối đa 30 cột hiển thị.");

        RuleForEach(x => x.Filters).SetValidator(new SavedViewFilterDtoValidator());
        RuleForEach(x => x.Columns).SetValidator(new SavedViewColumnDtoValidator());
    }
}

public class UpdateSavedViewRequestValidator : AbstractValidator<UpdateSavedViewRequest>
{
    public UpdateSavedViewRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên view không được để trống.")
            .MaximumLength(100).WithMessage("Tên view tối đa 100 ký tự.");

        RuleFor(x => x.SortBy).IsInEnum().When(x => x.SortBy.HasValue);
        RuleFor(x => x.GroupBy).IsInEnum().When(x => x.GroupBy.HasValue);

        RuleFor(x => x.Filters)
            .Must(f => f is null || f.Count <= 20)
            .WithMessage("Một view tối đa 20 điều kiện lọc.");

        RuleFor(x => x.Columns)
            .Must(c => c is null || c.Count <= 30)
            .WithMessage("Một view tối đa 30 cột hiển thị.");

        RuleForEach(x => x.Filters).SetValidator(new SavedViewFilterDtoValidator());
        RuleForEach(x => x.Columns).SetValidator(new SavedViewColumnDtoValidator());
    }
}

public class ReorderSavedViewsRequestValidator : AbstractValidator<ReorderSavedViewsRequest>
{
    public ReorderSavedViewsRequestValidator()
        => RuleFor(x => x.ViewIds)
            .NotEmpty().WithMessage("Danh sách sắp xếp không được rỗng.");
}

public class TaskQueryRequestValidator : AbstractValidator<TaskQueryRequest>
{
    public TaskQueryRequestValidator()
    {
        RuleFor(x => x.Filters)
            .Must(f => f is null || f.Count <= 20)
            .WithMessage("Tối đa 20 điều kiện lọc trong một truy vấn.");

        RuleFor(x => x.SortBy).IsInEnum().When(x => x.SortBy.HasValue);

        RuleForEach(x => x.Filters).SetValidator(new SavedViewFilterDtoValidator());

        // Page/PageSize không kiểm ở đây: PagedRequest tự kẹp về khoảng hợp lệ (trần 100),
        // nên một giá trị ngoài khoảng là chuyện đã được xử lý chứ không phải lỗi đầu vào.
    }
}
