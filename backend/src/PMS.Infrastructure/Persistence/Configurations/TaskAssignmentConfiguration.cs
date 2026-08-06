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

        // Index thứ hai, RIÊNG cho tra cứu theo EmployeeId một mình — index unique ở trên
        // có EmployeeId đứng thứ HAI nên không phục vụ được truy vấn dạng "mọi task của
        // nhân viên X" (TallyByAssigneeAsync group theo EmployeeId, và "Việc của tôi" lọc
        // theo EmployeeId). `IncludeProperties(TaskId)` biến nó thành covering index cho
        // đúng hai cột mà hai truy vấn đó cần, khỏi phải lookup ngược vào bảng chính.
        builder.HasIndex(ta => ta.EmployeeId).IncludeProperties(ta => ta.TaskId);

        builder.HasOne(ta => ta.Task)
               .WithMany(t => t.Assignments)
               .HasForeignKey(ta => ta.TaskId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ta => ta.Employee)
               .WithMany(e => e.TaskAssignments)
               .HasForeignKey(ta => ta.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(ta => !ta.Task.IsDeleted);
    }
}
