using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class NotficationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type).IsRequired();
        builder.Property(n => n.Content).IsRequired().HasMaxLength(500);
        builder.Property(n => n.IsRead).HasDefaultValue(false);

        builder.HasIndex(n => new { n.EmployeeId, n.IsRead });

        builder.HasOne(n => n.Recipient)
               .WithMany(e => e.Notifications)
               .HasForeignKey(n => n.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}