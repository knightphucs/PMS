using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public static class TaskAuthorizationExtensions
{
    /// <summary>
    /// Kiểm quyền tầng 2 trên project chứa task (ADR-019), và chuẩn hóa 404: task không
    /// tồn tại và task thuộc project mà người gọi không phải thành viên đều trả về cùng
    /// một thông báo. Nếu để nguyên NotFoundException(nameof(Project), ...) từ authz thì
    /// hai trường hợp phân biệt được nhau qua nội dung lỗi — đủ để người ngoài dò xem
    /// một taskId có tồn tại hay không (OWASP API1:2023).
    /// </summary>
    public static async Task<RoleInProject> AuthorizeTaskAsync(
        this IProjectAuthorizationService authz,
        TaskItem task, ProjectAction action, CancellationToken ct = default)
    {
        try
        {
            return await authz.AuthorizeAsync(task.ProjectId, action, ct);
        }
        catch (NotFoundException)
        {
            throw new NotFoundException(nameof(TaskItem), task.Id);
        }
    }
}
