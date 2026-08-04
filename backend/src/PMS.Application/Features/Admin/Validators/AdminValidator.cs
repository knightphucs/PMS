using FluentValidation;
using PMS.Application.Common.Authorization;

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

public class UpdateRolePermissionsRequestValidator : AbstractValidator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
        // NotNull chứ không NotEmpty: gỡ HẾT quyền của vai trò `User` là thao tác hợp lệ
        // (hệ quả: không ai tạo được project nữa, nhưng đó là lựa chọn của quản trị viên).
        // Bất biến duy nhất được bảo vệ nằm ở service — SystemAdmin phải giữ `roles:manage`.
        RuleFor(x => x.Permissions)
            .NotNull().WithMessage("Thiếu danh sách quyền.");

        RuleForEach(x => x.Permissions)
            .Must(code => SystemPermissions.All.Contains(code))
            .WithMessage("Mã quyền `{PropertyValue}` không tồn tại trong danh mục.");

        RuleFor(x => x.Permissions)
            .Must(list => list is null || list.Distinct().Count() == list.Count)
            .WithMessage("Danh sách quyền có mã trùng lặp.");
    }
}