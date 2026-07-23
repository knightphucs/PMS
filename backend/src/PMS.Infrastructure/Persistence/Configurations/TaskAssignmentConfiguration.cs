using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("TaskAssignments");
        builder.HasKey(ta => ta.Id);

        builder.Property(ta => ta.RoleInTask).IsRequired();

        // Chống gán trùng ở tầng DB (bổ trợ cho kiểm tra trong TaskItem.AddAssignee).
        builder.HasIndex(ta => new { ta.TaskId, ta.EmployeeId }).IsUnique();

        builder.HasOne(ta => ta.Task)
               .WithMany(t => t.Assignments)
               .HasForeignKey(ta => ta.TaskId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ta => ta.Employee)
               .WithMany(e => e.TaskAssignments)
               .HasForeignKey(ta => ta.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
