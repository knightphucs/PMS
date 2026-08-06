using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Key).IsRequired().HasMaxLength(10);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
        builder.Property(p => p.RowVersion).IsRowVersion();

        // Duy trì bởi trigger DB, không phải bởi C# — xem doc-comment ở Project.TaskCount.
        builder.Property(p => p.TaskCount).IsRequired().HasDefaultValue(0);

        builder.HasIndex(p => p.IsDeleted); // Global Query Filter

        // Không lọc theo IsDeleted — cùng lý do với IX_Tasks_ProjectId_Number: mã project
        // đã đi vào mã task (PMS-12) nên không bao giờ được tái sử dụng (ADR-033).
        builder.HasIndex(p => p.Key).IsUnique();

        builder.HasMany(p => p.Tasks)
               .WithOne(t => t.Project)
               .HasForeignKey(t => t.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Sprints)
               .WithOne(s => s.Project)
               .HasForeignKey(s => s.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
