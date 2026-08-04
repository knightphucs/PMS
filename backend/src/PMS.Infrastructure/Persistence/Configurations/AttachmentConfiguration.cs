using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        // CHECK constraint là chốt chặn cuối cho bất biến "đúng một chủ sở hữu" mà hai
        // factory Attachment.ForTask/ForProject giữ ở tầng domain (ADR-035). Phải dùng
        // overload lambda của ToTable — overload chuỗi mà 12 configuration kia đang dùng
        // không mang được constraint.
        builder.ToTable("Attachments", t => t.HasCheckConstraint(
            "CK_Attachments_ExactlyOneOwner",
            "([TaskId] IS NOT NULL AND [ProjectId] IS NULL) OR ([TaskId] IS NULL AND [ProjectId] IS NOT NULL)"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
        builder.Property(a => a.StoredFileName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(150);
        builder.Property(a => a.SizeBytes).IsRequired();

        builder.HasIndex(a => a.TaskId);
        builder.HasIndex(a => a.ProjectId);
        builder.HasIndex(a => a.StoredFileName).IsUnique();

        // 🔴 Task = Cascade nhưng Project = Restrict, KHÔNG phải tùy tiện: Project → Tasks
        // đã là Cascade, nên để cả hai FK ở đây cùng Cascade thì SQL Server thấy hai đường
        // cascade cùng đổ vào Attachments và từ chối migration bằng lỗi 1785 — đúng bức
        // tường đã buộc TaskItem.ParentTask/Reporter và cả hai FK của TaskLink dùng Restrict.
        // Trên thực tế không nhánh nào chạy: Project/Task đều xóa mềm, và ApplySoftDelete()
        // đổi state Deleted → Modified trước khi SaveChanges nên cascade của EF không kích
        // hoạt (bài học ADR-008).
        builder.HasOne(a => a.Task)
               .WithMany()
               .HasForeignKey(a => a.TaskId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Project)
               .WithMany()
               .HasForeignKey(a => a.ProjectId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Uploader)
               .WithMany()
               .HasForeignKey(a => a.UploaderId)
               .OnDelete(DeleteBehavior.Restrict);

        // 🔴 Nhánh `== null` là BẮT BUỘC, không phải phòng xa. CommentConfiguration viết
        // được `!c.Task.IsDeleted` vì Comment.TaskId là required. Ở đây TaskId nullable nên
        // EF sinh LEFT JOIN: với attachment của project thì `a.Task.IsDeleted` là NULL,
        // `!NULL` cũng là NULL, và dòng đó bị lọc khỏi MỌI query một cách im lặng.
        builder.HasQueryFilter(a =>
            (a.TaskId    == null || !a.Task!.IsDeleted) &&
            (a.ProjectId == null || !a.Project!.IsDeleted));
    }
}
