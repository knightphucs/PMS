using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class ApprovalRepository : Repository<Approval>, IApprovalRepository
{
    public ApprovalRepository(PmsDbContext context) : base(context) { }

    private DbSet<ApprovalPolicy> Policies => Context.Set<ApprovalPolicy>();

    // ---------- luật duyệt (cấu hình) ----------

    public void AddPolicy(ApprovalPolicy policy) => Policies.Add(policy);

    public void RemovePolicy(ApprovalPolicy policy) => Policies.Remove(policy);

    public async Task<IReadOnlyList<ApprovalPolicy>> ListPoliciesAsync(
        Guid projectId, CancellationToken ct = default)
        => await Policies
            .AsNoTracking()
            .Include(p => p.WorkItemType)
            .Include(p => p.TargetColumn)
            .Include(p => p.Approvers).ThenInclude(a => a.Employee)
            .AsSplitQuery()
            .Where(p => p.ProjectId == projectId)
            // Order không unique — tie-break bằng Id, cùng lý do đã ghi ở BoardColumn.Order.
            .OrderBy(p => p.Order).ThenBy(p => p.Id)
            .ToListAsync(ct);

    public async Task<ApprovalPolicy?> GetPolicyWithApproversAsync(
        Guid id, CancellationToken ct = default)
        // 🔴 KHÔNG AsNoTracking: đây là đường ghi của một quan hệ nhiều-nhiều. Xem XML doc ở
        // IApprovalRepository — AsNoTracking ở đây là vé thẳng tới "Violation of PRIMARY KEY".
        => await Policies
            .Include(p => p.WorkItemType)
            .Include(p => p.TargetColumn)
            .Include(p => p.Approvers).ThenInclude(a => a.Employee)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<ApprovalPolicy?> FindGateAsync(
        Guid projectId, Guid workItemTypeId, Guid targetColumnId, CancellationToken ct = default)
        => await Policies
            .AsNoTracking()
            .Include(p => p.Approvers)
            .FirstOrDefaultAsync(
                p => p.ProjectId == projectId
                  && p.WorkItemTypeId == workItemTypeId
                  && p.TargetColumnId == targetColumnId, ct);

    public async Task<bool> HasAnyGateForTypeAsync(
        Guid projectId, Guid workItemTypeId, CancellationToken ct = default)
        => await Policies
            .AsNoTracking()
            .AnyAsync(p => p.ProjectId == projectId && p.WorkItemTypeId == workItemTypeId, ct);

    // ---------- yêu cầu duyệt (dữ liệu chạy) ----------

    public async Task<Approval?> GetActiveAsync(
        Guid taskId, Guid policyId, CancellationToken ct = default)
        => await DbSet
            .Include(a => a.Decisions).ThenInclude(d => d.Approver)
            .Include(a => a.RequestedBy)
            .AsSplitQuery()
            .Where(a => a.TaskId == taskId && a.ApprovalPolicyId == policyId && a.ConsumedAt == null)
            .OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Approval>> ListByTaskAsync(
        Guid taskId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(a => a.Decisions).ThenInclude(d => d.Approver)
            .Include(a => a.RequestedBy)
            .Include(a => a.ApprovalPolicy).ThenInclude(p => p.TargetColumn)
            .Include(a => a.ApprovalPolicy).ThenInclude(p => p.Approvers).ThenInclude(x => x.Employee)
            .AsSplitQuery()
            .Where(a => a.TaskId == taskId)
            // Mới nhất lên đầu: hàng đang chặn (nếu có) luôn là hàng người đọc cần thấy trước.
            .OrderByDescending(a => a.RequestedAt).ThenByDescending(a => a.Id)
            .ToListAsync(ct);

    public async Task<Approval?> GetWithDecisionsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(a => a.Decisions).ThenInclude(d => d.Approver)
            .Include(a => a.RequestedBy)
            .Include(a => a.Task)
            .Include(a => a.ApprovalPolicy).ThenInclude(p => p.Approvers)
            // TargetColumn là bắt buộc, không phải tiện tay: ToResponse đọc
            // `policy.TargetColumn.Name`, và thiếu Include ở đây thì đường duyệt/huỷ trả về
            // một `targetColumnName` RỖNG — hỏng im lặng, không test nào khẳng định nó.
            .Include(a => a.ApprovalPolicy).ThenInclude(p => p.TargetColumn)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<int> DeleteApprovalsForPolicyAsync(
        Guid policyId, CancellationToken ct = default)
        // ApprovalDecisions biến mất theo nhờ FK Cascade từ Approvals.
        => await DbSet.Where(a => a.ApprovalPolicyId == policyId).ExecuteDeleteAsync(ct);

    public async Task<int> DeletePoliciesForAsync(
        Guid? workItemTypeId, Guid? boardColumnId, CancellationToken ct = default)
    {
        // Hai bước vì Approvals treo dưới ApprovalPolicies bằng Restrict (sơ đồ cascade ở
        // ApprovalConfigurations): xoá policy khi còn hàng Approvals trỏ tới nó là 500.
        //
        // ⚠️ Đây là chỗ DUY NHẤT trong hệ thống xoá một hàng Approvals. Nó có vẻ mâu thuẫn
        // với "Approvals là nhật ký kiểm toán", nhưng không: người dùng vừa xoá chính cái
        // LUẬT mà những chữ ký đó nói về. Giữ lại phiếu duyệt cho một cổng không còn tồn tại
        // là giữ một dòng kiểm toán không đọc được — và cột board/loại việc bị xoá cũng đã
        // mang theo ngữ cảnh của nó rồi.
        var policyIds = await Policies
            .Where(p => (workItemTypeId != null && p.WorkItemTypeId == workItemTypeId)
                     || (boardColumnId != null && p.TargetColumnId == boardColumnId))
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (policyIds.Count == 0) return 0;

        // ApprovalDecisions biến mất theo nhờ FK Cascade từ Approvals.
        await DbSet.Where(a => policyIds.Contains(a.ApprovalPolicyId)).ExecuteDeleteAsync(ct);

        // ApprovalPolicyApprovers biến mất theo nhờ FK Cascade từ ApprovalPolicies.
        return await Policies.Where(p => policyIds.Contains(p.Id)).ExecuteDeleteAsync(ct);
    }
}
