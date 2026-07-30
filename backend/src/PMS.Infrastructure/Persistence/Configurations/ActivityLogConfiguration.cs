using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(a => a.Detail).HasMaxLength(1000);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        
        builder.HasOne(a => a.Employee)
               .WithMany(a => a.ActivityLogs)
               .HasForeignKey(a => a.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}