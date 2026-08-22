using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// View lưu được (ADR-061).
///
/// <para>
/// 🔴 <b>Sơ đồ cascade, vẽ TRƯỚC khi chạy migration</b> — ADR-059 đã trả giá 18 test đỏ
/// trong 12ms cho việc bỏ qua bước này, ADR-060 làm đúng và tránh được:
/// </para>
/// <code>
/// Projects   ─ClientNoAction→ SavedViews       ─Cascade→ SavedViewFilters
/// Projects   ─ClientNoAction→ SavedViews       ─Cascade→ SavedViewColumns
/// Projects   ─ClientNoAction→ FieldDefinitions ─Cascade→ SavedViewFilters
/// Projects   ─ClientNoAction→ FieldDefinitions ─Cascade→ SavedViewColumns
/// Employees  ─Restrict──────→ SavedViews
/// </code>
/// <para>
/// <c>SavedViewFilters</c> nhận CASCADE từ hai bảng cha khác nhau, nhưng <b>không có gốc
/// chung nào cascade xuống theo hai lối</b>: cả <c>SavedViews</c> lẫn <c>FieldDefinitions</c>
/// đều treo dưới <c>Projects</c> bằng <c>ClientNoAction</c>. Đây đúng hình dạng an toàn mà
/// ADR-060 đã phân tích cho <c>WorkItemTypeFields</c>.
/// </para>
/// <para>
/// Đó cũng là lý do <see cref="SavedView.SortBy"/> chỉ nhận trường dựng sẵn — chi tiết ở
/// chính thuộc tính đó.
/// </para>
/// </summary>
public class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("SavedViews");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).IsRequired().HasMaxLength(100);
        builder.Property(v => v.IsShared).IsRequired();
        builder.Property(v => v.Order).IsRequired();
        builder.Property(v => v.SortBy).HasMaxLength(30).HasConversion<string>();
        builder.Property(v => v.GroupBy).HasMaxLength(30).HasConversion<string>();

        // Hình dạng truy vấn duy nhất: "mọi view của project này mà tôi được thấy, theo thứ tự".
        builder.HasIndex(v => new { v.ProjectId, v.Order });

        // Hai view trùng tên trong cùng một project làm thanh chọn view thành hai mục không
        // phân biệt được — cùng lý lẽ với FieldDefinitions.ProjectId+Label.
        //
        // ⚠️ Unique bao gồm cả OwnerId: hai người KHÁC nhau đặt trùng tên cho view RIÊNG của
        // họ là hợp lệ (họ không nhìn thấy view của nhau), còn cùng một người tạo hai view
        // trùng tên thì không.
        builder.HasIndex(v => new { v.ProjectId, v.OwnerId, v.Name }).IsUnique();

        builder.HasOne(v => v.Project)
               .WithMany(p => p.SavedViews)
               .HasForeignKey(v => v.ProjectId)
               // Project chỉ xóa MỀM nên EF không được đụng tới hàng con — cùng lý do đã ghi
               // dài ở BoardColumnConfiguration/FieldDefinitionConfiguration.
               .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(v => v.Owner)
               .WithMany()
               .HasForeignKey(v => v.OwnerId)
               // Restrict: Employee không có đường xoá cứng nào trong hệ thống (khoá tài
               // khoản chứ không xoá), nên đây thuần tuý là chốt chặn. Cascade sẽ là một
               // đường cascade thứ hai vào SavedViews mà không đổi lấy được gì.
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(v => !v.Project.IsDeleted);
    }
}

/// <summary>Một dòng điều kiện của view (ADR-061).</summary>
public class SavedViewFilterConfiguration : IEntityTypeConfiguration<SavedViewFilter>
{
    public void Configure(EntityTypeBuilder<SavedViewFilter> builder)
    {
        builder.ToTable("SavedViewFilters", t => t.HasCheckConstraint(
            "CK_SavedViewFilters_DungMotNguonTruong",
            "(CASE WHEN [Field]             IS NOT NULL THEN 1 ELSE 0 END" +
            " + CASE WHEN [FieldDefinitionId] IS NOT NULL THEN 1 ELSE 0 END) = 1"));

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Field).HasMaxLength(30).HasConversion<string>();
        builder.Property(f => f.Operator).IsRequired().HasMaxLength(30).HasConversion<string>();
        builder.Property(f => f.Value).HasMaxLength(200);

        builder.HasIndex(f => f.SavedViewId);

        builder.HasOne(f => f.SavedView)
               .WithMany(v => v.Filters)
               .HasForeignKey(f => f.SavedViewId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.FieldDefinition)
               .WithMany()
               .HasForeignKey(f => f.FieldDefinitionId)
               // 🔑 Cascade THẬT, và đây chính là lý do tồn tại của bảng này thay vì một cột
               // JSON: xoá một trường tuỳ biến thì mọi điều kiện trỏ vào nó biến mất theo,
               // do DB đảm bảo. JSON sẽ để lại một điều kiện trỏ vào hư không — im lặng,
               // không lệnh nào tìm ra, và view vẫn "chạy" nhưng trả kết quả sai.
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(f => !f.SavedView.Project.IsDeleted);
    }
}

/// <summary>Một cột hiển thị của view (ADR-061).</summary>
public class SavedViewColumnConfiguration : IEntityTypeConfiguration<SavedViewColumn>
{
    public void Configure(EntityTypeBuilder<SavedViewColumn> builder)
    {
        builder.ToTable("SavedViewColumns", t => t.HasCheckConstraint(
            "CK_SavedViewColumns_DungMotNguonTruong",
            "(CASE WHEN [Field]             IS NOT NULL THEN 1 ELSE 0 END" +
            " + CASE WHEN [FieldDefinitionId] IS NOT NULL THEN 1 ELSE 0 END) = 1"));

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Field).HasMaxLength(30).HasConversion<string>();
        builder.Property(c => c.Order).IsRequired();

        builder.HasIndex(c => new { c.SavedViewId, c.Order });

        builder.HasOne(c => c.SavedView)
               .WithMany(v => v.Columns)
               .HasForeignKey(c => c.SavedViewId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.FieldDefinition)
               .WithMany()
               .HasForeignKey(c => c.FieldDefinitionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => !c.SavedView.Project.IsDeleted);
    }
}
