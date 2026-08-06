using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IProjectInvitationRepository : IRepository<ProjectInvitation>
{
    /// <summary>
    /// Tra theo hash. Trả về <c>null</c> khi không tồn tại — service KHÔNG được phân biệt
    /// trường hợp này với "hết hạn"/"đã dùng", cả ba phải ra cùng một lỗi (mirror ADR-041).
    /// </summary>
    Task<ProjectInvitation?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Lời mời Pending (còn dùng được) hiện có cho một email trong một project — để vô hiệu khi mời lại.</summary>
    Task<ProjectInvitation?> GetPendingByProjectAndEmailAsync(
        Guid projectId, string email, CancellationToken ct = default);
}
