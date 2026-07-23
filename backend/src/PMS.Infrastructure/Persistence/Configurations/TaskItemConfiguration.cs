using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.Priority).IsRequired();
        builder.Property(t => t.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => t.SprintId);
        builder.HasIndex(t => t.ParentTaskId);
        builder.HasIndex(t => t.IsDeleted);

        builder.HasMany(t => t.Subtasks)
               .WithOne(t => t.ParentTask)
               .HasForeignKey(t => t.ParentTaskId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Reporter)
               .WithMany(e => e.ReportedTasks)
               .HasForeignKey(t => t.ReporterId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Labels)
               .WithMany(l => l.Tasks)
               .UsingEntity(j => j.ToTable("TaskLabels"));
    }
}
