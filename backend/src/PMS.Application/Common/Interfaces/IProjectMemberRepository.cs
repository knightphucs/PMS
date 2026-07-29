using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IProjectMemberRepository : IRepository<ProjectMember>
{
    Task<IReadOnlyList<ProjectMember>> GetPendingInvitationsAsync(
        Guid employeeId, CancellationToken ct = default);
}