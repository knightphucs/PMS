using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class WatcherConfiguration : IEntityTypeConfiguration<Watcher>
{
    public void Configure(EntityTypeBuilder<Watcher> builder)
    {
        builder.ToTable("Watchers");

        builder.HasKey(w => new { w.TaskId, w.EmployeeId });

        builder.HasOne(w => w.Task)
               .WithMany(t => t.Watchers)
               .HasForeignKey(w => w.TaskId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Employee)
               .WithMany(e => e.Watching)
               .HasForeignKey(w => w.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(w => !w.Task.IsDeleted);
    }
}
