using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class TaskLinkConfiguration : IEntityTypeConfiguration<TaskLink>
{
    public void Configure(EntityTypeBuilder<TaskLink> builder)
    {
        builder.ToTable("TaskLinks");
        builder.HasKey(tl => tl.Id);

        builder.Property(tl => tl.LinkType).IsRequired();

        builder.HasIndex(tl => new { tl.SourceTaskId, tl.TargetTaskId, tl.LinkType }).IsUnique();

        builder.HasOne(tl => tl.SourceTask)
               .WithMany(t => t.OutgoingLinks)
               .HasForeignKey(tl => tl.SourceTaskId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tl => tl.TargetTask)
               .WithMany(t => t.IncomingLinks)
               .HasForeignKey(tl => tl.TargetTaskId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(tl => !tl.SourceTask.IsDeleted && !tl.TargetTask.IsDeleted);
    }
}
