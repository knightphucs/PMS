using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Phê duyệt là dữ liệu (ADR-062).
///
/// <para>
/// 🔴 <b>Sơ đồ cascade, vẽ TRƯỚC khi chạy migration</b> — ADR-059 đã trả giá 18 test đỏ
/// trong 12ms cho việc bỏ qua bước này; ADR-060 và ADR-061 làm đúng và qua ngay lần đầu:
/// </para>
/// <code>
/// Projects       ─ClientNoAction─→ ApprovalPolicies ─Cascade─→ ApprovalPolicyApprovers
/// WorkItemTypes  ─Restrict───────→ ApprovalPolicies
/// BoardColumns   ─Restrict───────→ ApprovalPolicies
/// Employees      ─Restrict───────→ ApprovalPolicyApprovers
///
/// Tasks            ─Cascade──────→ Approvals        ─Cascade─→ ApprovalDecisions
/// ApprovalPolicies ─Restrict─────→ Approvals
/// Employees        ─Restrict─────→ Approvals            (RequestedById)
/// Employees        ─Restrict─────→ ApprovalDecisions
/// </code>
/// <para>
/// Phép kiểm là <i>"không bảng con nào nhận CASCADE từ một gốc chung theo HAI lối"</i> — và
/// SQL Server kiểm điều đó <b>tĩnh</b>, không quan tâm lối thứ hai có bao giờ chạy không:
/// </para>
/// <list type="bullet">
/// <item><c>ApprovalPolicies</c> <b>không nhận cascade từ đâu cả</b>. <c>Projects</c> là
/// <c>ClientNoAction</c> vì Project xoá MỀM (khuôn <see cref="SavedViewConfiguration"/>);
/// <c>WorkItemTypes</c>/<c>BoardColumns</c> là <c>Restrict</c> đúng khuôn
/// <c>TaskItemConfiguration</c> — xoá một loại hay một cột phải đi qua service để chọn đích,
/// không được cascade làm mất dữ liệu.</item>
/// <item><c>Approvals</c> nhận cascade <b>chỉ từ <c>Tasks</c></b> (khuôn
/// <c>CommentConfiguration</c>, kèm query filter <c>!Task.IsDeleted</c> vì Task xoá mềm).
/// Lối thứ hai — từ <c>ApprovalPolicies</c> — là <c>Restrict</c>, nên không gốc nào đi được
/// hai đường.</item>
/// </list>
/// <para>
/// ⚠️ <b>Cái giá của <c>Restrict</c>, và là điểm dễ quên nhất của cả hạng mục:</b> xoá một
/// <c>WorkItemType</c> hoặc <c>BoardColumn</c> đang có luật duyệt sẽ ném
/// <c>DbUpdateException</c> → <b>500</b>. <c>WorkItemTypeService.DeleteAsync</c> và
/// <c>BoardColumnService.DeleteAsync</c> dọn <c>ApprovalPolicy</c> liên quan trong cùng
/// transaction. Nó không đỏ lúc biên dịch và chỉ nổ ở một đường xoá mà bộ test đã đi qua sẵn.
/// </para>
/// </summary>
public class ApprovalPolicyConfiguration : IEntityTypeConfiguration<ApprovalPolicy>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicy> builder)
    {
        builder.ToTable("ApprovalPolicies", t => t.HasCheckConstraint(
            "CK_ApprovalPolicies_MinApprovalsDuong", "[MinApprovals] >= 1"));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ApproverMode).IsRequired().HasMaxLength(30).HasConversion<string>();
        builder.Property(p => p.MinApprovals).IsRequired();
        builder.Property(p => p.Order).IsRequired();

        // MỘT cổng = MỘT luật. Quorum đã phủ ca "cần 2 người duyệt"; duyệt nhiều chặng tuần
        // tự chưa ship (xem XML doc của ApprovalPolicy.Order). Unique ở đây là thứ khiến
        // guard chỉ phải tìm đúng một hàng thay vì phải diễn giải một thứ tự.
        builder.HasIndex(p => new { p.ProjectId, p.WorkItemTypeId, p.TargetColumnId }).IsUnique();

        builder.HasOne(p => p.Project)
               .WithMany(pr => pr.ApprovalPolicies)
               .HasForeignKey(p => p.ProjectId)
               // Project chỉ xoá MỀM nên EF không được đụng tới hàng con — cùng lý do đã ghi
               // dài ở BoardColumnConfiguration/SavedViewConfiguration.
               .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(p => p.WorkItemType)
               .WithMany()
               .HasForeignKey(p => p.WorkItemTypeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.TargetColumn)
               .WithMany()
               .HasForeignKey(p => p.TargetColumnId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => !p.Project.IsDeleted);
    }
}

/// <summary>Một người có tên trong danh sách duyệt của một cổng (ADR-062).</summary>
public class ApprovalPolicyApproverConfiguration : IEntityTypeConfiguration<ApprovalPolicyApprover>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicyApprover> builder)
    {
        builder.ToTable("ApprovalPolicyApprovers");

        // Khoá GHÉP — cùng khuôn Watcher (ADR-036) và WorkItemTypeField (ADR-060). Chặn
        // trùng bằng lược đồ thay vì bằng một unique index phải nhớ khai thêm.
        builder.HasKey(a => new { a.ApprovalPolicyId, a.EmployeeId });

        builder.HasOne(a => a.ApprovalPolicy)
               .WithMany(p => p.Approvers)
               .HasForeignKey(a => a.ApprovalPolicyId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Employee)
               .WithMany()
               .HasForeignKey(a => a.EmployeeId)
               // Restrict: Employee không có đường xoá cứng nào trong hệ thống (khoá tài
               // khoản chứ không xoá), nên đây thuần tuý là chốt chặn.
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => !a.ApprovalPolicy.Project.IsDeleted);
    }
}

/// <summary>MỘT lần đi qua một cổng duyệt (ADR-062) — là nhật ký, không phải trạng thái.</summary>
public class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("Approvals");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status).IsRequired().HasMaxLength(30).HasConversion<string>();
        builder.Property(a => a.RequestedAt).IsRequired();

        // Hình dạng truy vấn duy nhất của guard: "yêu cầu còn hiệu lực của task này trên
        // cổng này". ConsumedAt nằm trong khoá vì nó là thứ phân biệt hàng đang chặn với
        // hàng lịch sử — mà bảng này chỉ toàn hàng lịch sử sau vài tháng chạy.
        builder.HasIndex(a => new { a.TaskId, a.ApprovalPolicyId, a.ConsumedAt });

        builder.HasOne(a => a.Task)
               .WithMany(t => t.Approvals)
               .HasForeignKey(a => a.TaskId)
               // Cascade từ Task, đúng khuôn CommentConfiguration. Đây là lối cascade DUY
               // NHẤT vào bảng này — xem sơ đồ ở đầu file.
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ApprovalPolicy)
               .WithMany()
               .HasForeignKey(a => a.ApprovalPolicyId)
               // 🔴 Restrict, KHÔNG cascade. Hai lý do, cả hai đều đủ để một mình quyết định:
               // (1) cascade ở đây là lối thứ hai vào Approvals -> SQL Server từ chối tạo FK;
               // (2) xoá một luật duyệt không được xoá mất vết "ai đã ký" của những lần đã
               //     đi qua. ApprovalPolicyService dọn Approvals trước khi xoá policy.
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.RequestedBy)
               .WithMany()
               .HasForeignKey(a => a.RequestedById)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => !a.Task.IsDeleted);
    }
}

/// <summary>Một lá phiếu trên một yêu cầu duyệt (ADR-062).</summary>
public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("ApprovalDecisions");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Decision).IsRequired().HasMaxLength(20).HasConversion<string>();
        builder.Property(d => d.Comment).HasMaxLength(1000);
        builder.Property(d => d.DecidedAt).IsRequired();

        // Một người một phiếu. Approval.AddDecision cũng kiểm để trả 409 có thông điệp đọc
        // được thay vì để index này ném DbUpdateException -> 500 (khuôn SavedViewService);
        // index vẫn cần vì nó là chốt chặn thật khi hai request về cùng lúc.
        builder.HasIndex(d => new { d.ApprovalId, d.ApproverId }).IsUnique();

        builder.HasOne(d => d.Approval)
               .WithMany(a => a.Decisions)
               .HasForeignKey(d => d.ApprovalId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Approver)
               .WithMany()
               .HasForeignKey(d => d.ApproverId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(d => !d.Approval.Task.IsDeleted);
    }
}
