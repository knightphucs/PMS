using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class FieldDefinitionRepository : Repository<FieldDefinition>, IFieldDefinitionRepository
{
    public FieldDefinitionRepository(PmsDbContext context) : base(context) { }

    public async Task<IReadOnlyList<FieldDefinition>> ListByProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(f => f.Options.OrderBy(o => o.Order).ThenBy(o => o.Id))
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Order).ThenBy(f => f.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FieldDefinition>> ListByProjectWithTrackingAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(f => f.Options.OrderBy(o => o.Order).ThenBy(o => o.Id))
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Order).ThenBy(f => f.Id)
            .ToListAsync(ct);

    public async Task<FieldDefinition?> GetWithOptionsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(f => f.Options.OrderBy(o => o.Order).ThenBy(o => o.Id))
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountValuesByFieldAsync(
        Guid projectId, CancellationToken ct = default)
        => await Context.FieldValues
            .AsNoTracking()
            .Where(v => v.FieldDefinition.ProjectId == projectId)
            .GroupBy(v => v.FieldDefinitionId)
            .Select(g => new { FieldId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FieldId, x => x.Count, ct);

    public async Task<IReadOnlyList<FieldValue>> ListValuesForTaskAsync(
        Guid taskId, CancellationToken ct = default)
        => await Context.FieldValues
            // KHÔNG AsNoTracking: SetValuesAsync sửa chính các entity này tại chỗ, kể cả
            // tập SelectedOptions. Đọc no-tracking rồi Attach lại sẽ làm EF không thấy được
            // phần tử nào vừa bị GỠ khỏi bảng nối — many-to-many chỉ sinh DELETE khi nó
            // theo dõi được trạng thái TRƯỚC của tập hợp.
            .Include(v => v.FieldDefinition)
            .Include(v => v.SelectedOptions)
            .Where(v => v.TaskId == taskId)
            .ToListAsync(ct);

    public async Task<int> DeleteValuesOfFieldAsync(
        Guid fieldDefinitionId, CancellationToken ct = default)
        // ExecuteDeleteAsync: một lệnh DELETE thẳng xuống DB. Một trường có thể có giá trị
        // trên hàng nghìn task, và thao tác này chạy trong lúc người dùng đang chờ dialog
        // xoá trường đóng lại — cùng lý do MoveAllTasksAsync của cột board không tải entity.
        => await Context.FieldValues
            .Where(v => v.FieldDefinitionId == fieldDefinitionId)
            .ExecuteDeleteAsync(ct);
}
