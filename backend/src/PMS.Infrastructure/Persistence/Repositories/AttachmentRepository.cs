using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class AttachmentRepository : Repository<Attachment>, IAttachmentRepository
{
    public AttachmentRepository(PmsDbContext context) : base(context) { }

    // KHÔNG AsNoTracking: DeleteAsync gọi method này rồi Remove entity trả về.
    public async Task<Attachment?> GetWithUploaderAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(a => a.Uploader)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Attachment>> ListByTaskAsync(
        Guid taskId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(a => a.Uploader)
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Attachment>> ListByProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(a => a.Uploader)
            .Where(a => a.ProjectId == projectId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
}
