using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;

namespace PMS.Application.Features.Employees;

public interface IEmployeeLookupService
{
    Task<IReadOnlyList<EmployeeLookupResponse>> SearchAsync(
        string? keyword, CancellationToken ct = default);
}

/// <summary>
/// Tra nhân viên cho ô gợi ý khi mời thành viên (ADR-046, tầng 3).
///
/// <para>
/// **Bối cảnh:** trước 2026-08-04 endpoint duy nhất liệt kê nhân viên là
/// <c>GET /admin/employees</c>, nằm sau quyền <c>employees:manage</c>. Nghĩa là một PM bình
/// thường muốn mời ai vào dự án thì phải **gõ đúng địa chỉ email** — không có cách nào tra.
/// </para>
/// <para>
/// 🔴 **Hai ràng buộc là lý do tồn tại của service này, không phải chi tiết cài đặt.** Bỏ
/// một trong hai là biến nó thành API dump toàn bộ danh bạ công ty cho bất kỳ ai có tài
/// khoản — chính xác thứ mà việc gác <c>GET /admin/employees</c> đang cố ngăn.
/// </para>
/// </summary>
public class EmployeeLookupService : IEmployeeLookupService
{
    /// <summary>
    /// Dưới ngưỡng này thì từ khóa không còn là "tra cứu" mà là "liệt kê": một ký tự đơn lẻ
    /// khớp với phần lớn danh bạ, và lặp 26 lần là có toàn bộ.
    /// </summary>
    private const int MinKeywordLength = 2;

    /// <summary>
    /// Trần cứng, KHÔNG nhận từ client. Ô gợi ý chỉ hiện được vài dòng; cho client tự chọn
    /// số lượng là mở lại đúng cánh cửa mà <see cref="MinKeywordLength"/> vừa khép.
    /// </summary>
    private const int MaxResults = 10;

    private readonly IUnitOfWork _uow;

    public EmployeeLookupService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<EmployeeLookupResponse>> SearchAsync(
        string? keyword, CancellationToken ct = default)
    {
        var trimmed = keyword?.Trim() ?? string.Empty;

        // 400 chứ không phải trả danh sách rỗng: rỗng sẽ khiến người dùng tưởng "không có
        // ai tên vậy", trong khi thật ra họ mới gõ chưa đủ.
        if (trimmed.Length < MinKeywordLength)
            throw new BusinessRuleException(
                $"Nhập tối thiểu {MinKeywordLength} ký tự để tìm nhân sự.");

        var found = await _uow.Employees.SearchActiveAsync(trimmed, MaxResults, ct);

        return found.Select(e => new EmployeeLookupResponse(e.Id, e.Name, e.Email)).ToList();
    }
}
