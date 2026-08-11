using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

/// <summary>Trường tuỳ biến — khai báo (ADR-059).</summary>
public class FieldDefinitionConfiguration : IEntityTypeConfiguration<FieldDefinition>
{
    public void Configure(EntityTypeBuilder<FieldDefinition> builder)
    {
        builder.ToTable("FieldDefinitions");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Label).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Type).IsRequired().HasMaxLength(20).HasConversion<string>();
        builder.Property(f => f.Order).IsRequired();

        // Hình dạng truy vấn duy nhất: "mọi trường của project này, theo thứ tự".
        builder.HasIndex(f => new { f.ProjectId, f.Order });

        // Hai trường trùng tên trong một project làm khối "Trường tuỳ biến" ở chi tiết task
        // thành hai ô không phân biệt được — người dùng không có cách nào biết ô nào là ô nào.
        builder.HasIndex(f => new { f.ProjectId, f.Label }).IsUnique();

        builder.HasOne(f => f.Project)
               .WithMany(p => p.FieldDefinitions)
               .HasForeignKey(f => f.ProjectId)
               // Cùng lý do đã ghi dài ở BoardColumnConfiguration: Project chỉ xóa MỀM, nên
               // EF không được đụng gì tới các hàng con khi project bị đánh dấu xóa. Cascade
               // và Restrict đều đã thử và đều ném 500 ở đó — đừng đi lại.
               .OnDelete(DeleteBehavior.ClientNoAction);

        // Soi gương query filter của Project, dù FieldDefinition không phải ISoftDeletable:
        // nó sống chết theo project chứ không có vòng đời xóa mềm riêng.
        builder.HasQueryFilter(f => !f.Project.IsDeleted);
    }
}

/// <summary>Lựa chọn của trường kiểu Select (ADR-059).</summary>
public class FieldOptionConfiguration : IEntityTypeConfiguration<FieldOption>
{
    public void Configure(EntityTypeBuilder<FieldOption> builder)
    {
        builder.ToTable("FieldOptions");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Label).IsRequired().HasMaxLength(100);
        builder.Property(o => o.Color).IsRequired().HasMaxLength(7);
        builder.Property(o => o.Order).IsRequired();

        builder.HasIndex(o => new { o.FieldDefinitionId, o.Order });
        builder.HasIndex(o => new { o.FieldDefinitionId, o.Label }).IsUnique();

        builder.HasOne(o => o.FieldDefinition)
               .WithMany(f => f.Options)
               .HasForeignKey(o => o.FieldDefinitionId)
               // Cascade THẬT ở đây, khác hẳn quan hệ với Project: xoá một trường thì các
               // lựa chọn của nó không còn nghĩa gì. FieldDefinition không xóa mềm nên
               // không dính cái bẫy ClientNoAction ở trên.
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(o => !o.FieldDefinition.Project.IsDeleted);
    }
}

/// <summary>Giá trị trường tuỳ biến trên một task (ADR-059).</summary>
public class FieldValueConfiguration : IEntityTypeConfiguration<FieldValue>
{
    public void Configure(EntityTypeBuilder<FieldValue> builder)
    {
        builder.ToTable("FieldValues", t => t.HasCheckConstraint(
            "CK_FieldValues_MotGiaTriDuyNhat",
            "(CASE WHEN ValueText    IS NOT NULL THEN 1 ELSE 0 END" +
            " + CASE WHEN ValueNumber  IS NOT NULL THEN 1 ELSE 0 END" +
            " + CASE WHEN ValueDate    IS NOT NULL THEN 1 ELSE 0 END" +
            " + CASE WHEN ValueBoolean IS NOT NULL THEN 1 ELSE 0 END) <= 1"));

        builder.HasKey(v => v.Id);

        builder.Property(v => v.ValueText).HasMaxLength(2000);
        // decimal có kiểu rõ ràng: mặc định của EF cho decimal trên SQL Server là (18,2) và
        // nó LÀM TRÒN im lặng. Với một trường người dùng tự khai (giờ công, tỷ lệ…) thì mất
        // chữ số thập phân mà không có cảnh báo nào là hỏng dữ liệu, không phải làm tròn.
        builder.Property(v => v.ValueNumber).HasPrecision(18, 4);

        // Một task chỉ có MỘT giá trị cho mỗi trường. Không có ràng buộc này thì một lần
        // double-submit sinh hai hàng, và "giá trị của trường X" thành câu hỏi mơ hồ.
        builder.HasIndex(v => new { v.TaskId, v.FieldDefinitionId }).IsUnique();

        builder.HasOne(v => v.Task)
               .WithMany(t => t.FieldValues)
               .HasForeignKey(v => v.TaskId)
               // TaskItem xóa MỀM -> cùng lý do ClientNoAction như trên.
               .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(v => v.FieldDefinition)
               .WithMany(f => f.Values)
               .HasForeignKey(v => v.FieldDefinitionId)
               // 🔴 `Restrict`, dù nghiệp vụ ĐÚNG LÀ "xoá trường thì giá trị biến mất theo".
               //
               // Cascade ở đây tạo HAI đường cascade từ FieldDefinitions xuống bảng nối
               // FieldValueOptions:
               //     FieldDefinitions -> FieldOptions -> FieldValueOptions
               //     FieldDefinitions -> FieldValues  -> FieldValueOptions
               // và SQL Server từ chối thẳng lúc tạo FK: *"may cause cycles or multiple
               // cascade paths"*. Đã gặp thật — migration áp được ở bước sinh SQL nhưng
               // CreateTable ném ngay, làm cả 18 test đỏ cùng lúc trong 12ms.
               //
               // Cắt đường thứ hai và cho `CustomFieldService.DeleteAsync` xoá giá trị một
               // cách TƯỜNG MINH trước khi xoá trường. Đắt hơn một dòng cấu hình, nhưng đổi
               // lại thứ tự xoá là thứ đọc được trong code chứ không phải thứ phải tin vào
               // engine, và số hàng bị xoá ghi được vào ActivityLog.
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.SelectedOptions)
               .WithMany(o => o.Values)
               .UsingEntity(j => j.ToTable("FieldValueOptions"));

        builder.HasQueryFilter(v => !v.Task.IsDeleted && !v.FieldDefinition.Project.IsDeleted);
    }
}
