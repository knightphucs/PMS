using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

/// <summary>Loại công việc của một project (ADR-060).</summary>
public class WorkItemTypeConfiguration : IEntityTypeConfiguration<WorkItemType>
{
    public void Configure(EntityTypeBuilder<WorkItemType> builder)
    {
        builder.ToTable("WorkItemTypes");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Icon).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Color).IsRequired().HasMaxLength(7);
        builder.Property(t => t.Order).IsRequired();

        // Cổng yêu cầu (ADR-063). Không cần backfill: `false` đúng cho mọi hàng có sẵn —
        // trước ADR-063 không loại nào nhận yêu cầu từ ngoài, nên default chính là sự thật
        // lịch sử chứ không phải một phỏng đoán.
        builder.Property(t => t.IsRequestable).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.RequestInstructions).HasMaxLength(2000);

        builder.HasIndex(t => new { t.ProjectId, t.Order });

        // Cổng yêu cầu hỏi đúng một câu ở tầng dữ liệu — "project nào có loại nhận yêu cầu"
        // — và nó chạy trên MỌI lần mở /requests/new của MỌI người trong công ty, kể cả
        // người không thuộc project nào. Lọc trước theo cờ rồi mới gom project.
        builder.HasIndex(t => t.IsRequestable)
               .HasFilter("[IsRequestable] = 1");

        // Hai loại trùng tên trong một project làm ô chọn "chuyển task sang loại nào" thành
        // một câu hỏi không trả lời được — y hệt lý do cột board có ràng buộc này.
        builder.HasIndex(t => new { t.ProjectId, t.Name }).IsUnique();

        builder.HasOne(t => t.Project)
               .WithMany(p => p.WorkItemTypes)
               .HasForeignKey(t => t.ProjectId)
               // Project chỉ xoá MỀM — EF không được đụng gì tới hàng con. Lý do đầy đủ đã
               // ghi ở BoardColumnConfiguration (Cascade và Restrict đều đã thử và đều 500).
               .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasQueryFilter(t => !t.Project.IsDeleted);
    }
}

/// <summary>
/// Bảng nối loại ↔ trường tuỳ biến (ADR-060).
///
/// <para>
/// 🔴 <b>Phân tích đường cascade trước khi viết — bài học ADR-059.</b> Ở đó một FK
/// <c>Cascade</c> tạo ra hai đường xuống bảng nối và SQL Server từ chối tạo khoá ngoại,
/// làm 18 test đỏ cùng lúc. Bảng này có hai FK, và cả hai đều <c>Cascade</c> — an toàn vì
/// hai gốc <c>WorkItemTypes</c> và <c>FieldDefinitions</c> KHÔNG có đường cascade nào nối
/// tới nhau: cả hai đều treo dưới <c>Projects</c> bằng <c>ClientNoAction</c> (NO ACTION ở
/// tầng DB), nên không tồn tại một gốc chung nào cascade xuống đây theo hai lối.
/// </para>
/// </summary>
public class WorkItemTypeFieldConfiguration : IEntityTypeConfiguration<WorkItemTypeField>
{
    public void Configure(EntityTypeBuilder<WorkItemTypeField> builder)
    {
        builder.ToTable("WorkItemTypeFields");

        // Khoá GHÉP, không surrogate: cặp này vốn đã là định danh tự nhiên, và khoá ghép
        // chặn trùng bằng lược đồ thay vì bằng một unique index phải nhớ khai thêm.
        // Cùng khuôn Watcher (ADR-036).
        builder.HasKey(f => new { f.WorkItemTypeId, f.FieldDefinitionId });

        builder.Property(f => f.IsRequired).IsRequired();
        builder.Property(f => f.Order).IsRequired();

        builder.HasIndex(f => new { f.WorkItemTypeId, f.Order });

        builder.HasOne(f => f.WorkItemType)
               .WithMany(t => t.Fields)
               .HasForeignKey(f => f.WorkItemTypeId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.FieldDefinition)
               .WithMany()
               .HasForeignKey(f => f.FieldDefinitionId)
               // Xoá một trường thì mọi chỗ gắn nó biến mất theo. Khác FieldValues (ở đó
               // phải hạ xuống Restrict vì đường cascade thứ hai) — xem phân tích ở trên.
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(f => !f.WorkItemType.Project.IsDeleted);
    }
}
