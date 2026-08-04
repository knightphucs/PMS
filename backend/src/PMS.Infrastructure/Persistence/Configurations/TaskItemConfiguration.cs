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
        builder.Property(t => t.Description).HasMaxLength(4000);
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.Priority).IsRequired();
        builder.Property(t => t.Number).IsRequired();
        builder.Property(t => t.IsDeleted).HasDefaultValue(false);
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => t.SprintId);
        builder.HasIndex(t => t.ParentTaskId);
        builder.HasIndex(t => t.IsDeleted);

        // Chốt chặn cuối cho việc đánh số (ADR-033). CỐ Ý không lọc theo IsDeleted: số của
        // task đã xóa mềm vẫn phải giữ chỗ, vì mã PMS-12 đã phát tán ra comment/URL/tài liệu
        // ngoài hệ thống — cấp lại số đó cho task khác là làm sai lệch mọi tham chiếu cũ.
        builder.HasIndex(t => new { t.ProjectId, t.Number }).IsUnique();

        // Index THẬT SỰ còn thiếu (thêm 2026-08-04). Mọi cột khóa ngoại khác trong dự án đã
        // có index do EF Core tự sinh theo quy ước — đã kiểm bằng `sys.indexes` trên database
        // thật, không suy đoán. Cái này khác: nó là index GHÉP trên hai cột thường, nên không
        // quy ước nào tạo hộ.
        //
        // Vì sao cần: `DueDateNotificationWorker` quét theo hạn + trạng thái ở MỖI nhịp timer
        // (ADR-040). Không có nó thì công việc nền quét toàn bộ bảng Tasks, đều đặn, mãi mãi
        // — chi phí lớn dần theo dữ liệu mà không bao giờ có ai nhận ra vì nó chạy im lặng.
        builder.HasIndex(t => new { t.DueDate, t.Status });

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
