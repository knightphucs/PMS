using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class ProjectTaskCounterRepository : IProjectTaskCounterRepository
{
    private readonly PmsDbContext _context;

    public ProjectTaskCounterRepository(PmsDbContext context) => _context = context;

    public void Add(ProjectTaskCounter counter) => _context.ProjectTaskCounters.Add(counter);

    /// <summary>
    /// Một câu lệnh duy nhất: tăng và đọc lại giá trị mới trong cùng một thao tác nguyên tử.
    /// <para>
    /// Vì sao là SQL thô chứ không phải load-sửa-SaveChanges: đọc rồi ghi ở hai bước là
    /// một race kinh điển (hai người cùng đọc 5, cùng ghi 6). Còn dùng
    /// <c>Projects.RowVersion</c> làm khóa lạc quan thì lại phá ADR-016 — mỗi lần tạo task
    /// sẽ vô hiệu hóa token mà form sửa project đang round-trip.
    /// </para>
    /// <para>
    /// Vì sao KHÔNG vi phạm lệnh cấm bulk-update của ADR-024: lệnh cấm đó bảo vệ
    /// <c>ApplySoftDelete()</c>/<c>ApplyAuditFields()</c> khỏi bị bỏ qua. Hàng
    /// <c>ProjectTaskCounters</c> không có cột audit lẫn cờ soft-delete, nên ở đây không có
    /// interceptor nào để bỏ qua.
    /// </para>
    /// </summary>
    public async Task<int> NextNumberAsync(Guid projectId, CancellationToken ct = default)
    {
        var rows = await _context.Database
            .SqlQuery<int>($@"
                UPDATE ProjectTaskCounters
                SET NextNumber = NextNumber + 1
                OUTPUT INSERTED.NextNumber AS [Value]
                WHERE ProjectId = {projectId}")
            .ToListAsync(ct);

        // Không có hàng nào = project chưa có bộ đếm. Chỉ xảy ra với project tạo trước
        // migration mà backfill bỏ sót; tự vá tại chỗ thay vì để người dùng nhận 500.
        if (rows.Count == 0)
        {
            var maxExisting = await _context.Tasks
                .IgnoreQueryFilters()
                .Where(t => t.ProjectId == projectId)
                .Select(t => (int?)t.Number)
                .MaxAsync(ct) ?? 0;

            var next = maxExisting + 1;
            _context.ProjectTaskCounters.Add(
                new ProjectTaskCounter { ProjectId = projectId, NextNumber = next });
            await _context.SaveChangesAsync(ct);
            return next;
        }

        return rows[0];
    }
}
