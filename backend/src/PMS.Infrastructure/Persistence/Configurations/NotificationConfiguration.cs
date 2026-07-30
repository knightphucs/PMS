using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(n => n.Content).IsRequired().HasMaxLength(500);
        builder.Property(n => n.IsRead).HasDefaultValue(false);

        // Phục vụ hai truy vấn nóng của hộp thông báo: đếm chưa đọc và đánh dấu tất cả đã
        // đọc. Sort mặc định của danh sách là CreatedAt DESC nên phần sort chưa được index
        // phủ — chấp nhận ở quy mô đồ án, ghi lại ở §15 (giới hạn đã biết của ADR-023) thay
        // vì thêm index thứ hai làm chậm đường ghi vốn chạy ở MỌI luồng nghiệp vụ.
        builder.HasIndex(n => new { n.EmployeeId, n.IsRead });

        builder.HasOne(n => n.Recipient)
               .WithMany(e => e.Notifications)
               .HasForeignKey(n => n.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}