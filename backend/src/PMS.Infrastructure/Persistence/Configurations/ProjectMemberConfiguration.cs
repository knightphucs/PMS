using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");
        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.RoleInProject).IsRequired();
        builder.Property(pm => pm.InvitationStatus).IsRequired();

        builder.HasIndex(pm => new { pm.ProjectId, pm.EmployeeId }).IsUnique();

        builder.HasOne(pm => pm.Project)
               .WithMany(p => p.Members)
               .HasForeignKey(pm => pm.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pm => pm.Employee)
               .WithMany(e => e.ProjectMemberships)
               .HasForeignKey(pm => pm.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(pm => !pm.Project.IsDeleted);
    }
}
