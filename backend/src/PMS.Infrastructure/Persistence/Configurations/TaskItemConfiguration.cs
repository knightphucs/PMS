using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        // 🔴 `HasTrigger` là BẮT BUỘC sau khi thêm `trg_Tasks_MaintainProjectTaskCount`
        // (migration AddReportingDbObjects), không phải trang trí. Không khai báo thì mọi
        // INSERT/UPDATE qua EF vào bảng này NÉM `DbUpdateException` ngay lập tức: EF Core
        // mặc định sinh `OUTPUT INSERTED.RowVersion` để đọc lại giá trị `rowversion` vừa
        // ghi, và SQL Server CẤM `OUTPUT` không có `INTO` trên bảng có trigger đang bật —
        // lỗi 334, không liên quan gì tới logic của trigger. Khai báo này báo cho provider
        // chuyển sang chiến lược `OUTPUT INTO @bảng_tạm`, né đúng giới hạn đó.
        // Xem https://aka.ms/efcore-docs-sqlserver-save-changes-and-output-clause.
        builder.ToTable("Tasks", t => t.HasTrigger("trg_Tasks_MaintainProjectTaskCount"));
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(4000);
        builder.Property(t => t.Category).IsRequired();
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
        //
        // 📌 Cột thứ hai đổi từ `Status` sang `Category` cùng ADR-052. Chính vì `Category`
        // được lưu cứng TRÊN Tasks (chứ không phải đọc qua BoardColumn) mà index ghép này
        // còn dùng được nguyên vẹn — nếu phải JOIN sang bảng cột thì nó mất tác dụng.
        builder.HasIndex(t => new { t.DueDate, t.Category });

        builder.HasOne(t => t.BoardColumn)
               .WithMany(c => c.Tasks)
               .HasForeignKey(t => t.BoardColumnId)
               // Restrict: xóa cột phải đi qua BoardColumnService để chọn cột đích cho task
               // (ADR-052). Cascade ở đây sẽ xóa sạch task theo cột — mất dữ liệu vì một cú
               // bấm đổi cấu hình board.
               .OnDelete(DeleteBehavior.Restrict);

        // 🔴 AUTO-INCLUDE, và đây là lựa chọn có cân nhắc chứ không phải tiện tay.
        //
        // `TaskMapper.ToStatusRef` đọc `task.BoardColumn.Name/Color/Category`, tức là MỌI
        // query nuôi mapper — board, backlog, phân trang, chi tiết, subtask, task liên kết,
        // 10+ chuỗi Include rải khắp TaskRepository — đều phải nhớ thêm một Include nữa.
        // Quên một chỗ thì không có gì đỏ lúc biên dịch; nó nổ NullReferenceException ở
        // đúng cái request mà không ai test tới.
        //
        // Dự án này đã trả giá cho đúng hình dạng lỗi đó ít nhất ba lần: `SubtaskProgress`
        // luôn trả 0 vì ba query board/backlog thiếu Include, `Assignee.Employee` NRE, và
        // endpoint thống kê hỏng 500 từ ngày viết. Điểm chung: thứ cần kiểm chứng chưa có ai
        // gọi tới. AutoInclude biến "phải nhớ" thành "mặc định đúng".
        //
        // Giá phải trả: một INNER JOIN thừa ở vài query không dùng tới tên cột. Chấp nhận —
        // BoardColumns là bảng rất nhỏ (vài hàng mỗi project) và join theo khóa chính.
        // Query nào thật sự cần né thì gọi `.IgnoreAutoIncludes()`.
        builder.Navigation(t => t.BoardColumn).AutoInclude();

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
