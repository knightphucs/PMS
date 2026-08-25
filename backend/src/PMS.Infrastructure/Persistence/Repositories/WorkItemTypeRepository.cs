using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class WorkItemTypeRepository : Repository<WorkItemType>, IWorkItemTypeRepository
{
    public WorkItemTypeRepository(PmsDbContext context) : base(context) { }

    public async Task<IReadOnlyList<WorkItemType>> ListByProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(t => t.Fields.OrderBy(f => f.Order))
                .ThenInclude(f => f.FieldDefinition)
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Order).ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<WorkItemType?> GetWithFieldsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(t => t.Fields.OrderBy(f => f.Order))
                .ThenInclude(f => f.FieldDefinition)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<WorkItemType?> GetDefaultForProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Order).ThenBy(t => t.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountTasksByTypeAsync(
        Guid projectId, CancellationToken ct = default)
        => await Context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .GroupBy(t => t.WorkItemTypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TypeId, x => x.Count, ct);

    public async Task<int> MoveAllTasksAsync(
        Guid fromTypeId, Guid toTypeId, CancellationToken ct = default)
        // ExecuteUpdateAsync: một lệnh UPDATE thẳng xuống DB. Khác MoveAllTasksAsync của
        // cột board, ở đây KHÔNG có bản sao nào phải đồng bộ kèm — loại không có trường
        // "Category" song sinh trên TaskItem, nên một cột là đủ.
        => await Context.Tasks
            .Where(t => t.WorkItemTypeId == fromTypeId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.WorkItemTypeId, toTypeId), ct);

    public async Task<IReadOnlyList<WorkItemTypeField>> ListFieldsOfTaskTypeAsync(
        Guid taskId, CancellationToken ct = default)
        => await Context.WorkItemTypeFields
            .AsNoTracking()
            .Include(f => f.FieldDefinition)
                .ThenInclude(d => d.Options.OrderBy(o => o.Order).ThenBy(o => o.Id))
            .Where(f => Context.Tasks
                .Where(t => t.Id == taskId)
                .Select(t => t.WorkItemTypeId)
                .Contains(f.WorkItemTypeId))
            .OrderBy(f => f.Order)
            .ToListAsync(ct);

    // ---------- Cổng yêu cầu (ADR-063) ----------

    public async Task<IReadOnlyList<WorkItemType>> ListRequestableAsync(CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            // HasQueryFilter trên WorkItemType đã loại project xoá mềm — không lặp lại ở đây,
            // và cũng không được bỏ nó đi: cổng yêu cầu mà chào mời một project đã xoá thì
            // người gửi điền xong cả form mới nhận 404.
            .Include(t => t.Project)
            .Where(t => t.IsRequestable)
            .OrderBy(t => t.Project.Name).ThenBy(t => t.Order).ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WorkItemType>> ListRequestableByProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            // 🔴 Project BẮT BUỘC: GetFormAsync đọc `types[0].Project.Name` để dựng tiêu đề
            // form. Thiếu Include ở đây là NRE → 500 ở MỌI lần mở form, và build vẫn sạch.
            // Đã trả giá đúng một lần trong phiên ADR-063 — cùng lớp lỗi với bẫy "đặt khoá
            // ngoại mà quên navigation" (ADR-060), chỉ khác là ở đường ĐỌC thay vì đường GHI.
            .Include(t => t.Project)
            .Include(t => t.Fields.OrderBy(f => f.Order))
                .ThenInclude(f => f.FieldDefinition)
                    .ThenInclude(d => d.Options.OrderBy(o => o.Order).ThenBy(o => o.Id))
            .AsSplitQuery()
            .Where(t => t.ProjectId == projectId && t.IsRequestable)
            .OrderBy(t => t.Order).ThenBy(t => t.Id)
            .ToListAsync(ct);
}
