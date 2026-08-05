using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprints");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Goal).HasMaxLength(500);
        builder.Property(s => s.IsDeleted).HasDefaultValue(false);
        builder.Property(s => s.Status).IsRequired();

        builder.HasIndex(s => s.ProjectId);
        // Tra "sprint đang chạy của project này" là truy vấn nóng của mọi màn Sprint/Backlog.
        builder.HasIndex(s => new { s.ProjectId, s.Status });
        builder.HasIndex(s => s.IsDeleted);

        // Restrict: nếu SetNull sẽ tạo 2 đường cascade từ Projects tới Tasks
        // (trực tiếp + qua Sprints) -> SQL Server lỗi 1785.
        // Nghiệp vụ: phải chuyển Task về Backlog trước khi xóa Sprint.
        builder.HasMany(s => s.Tasks)
               .WithOne(t => t.Sprint)
               .HasForeignKey(t => t.SprintId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(s => !s.Project.IsDeleted);
    }
}
