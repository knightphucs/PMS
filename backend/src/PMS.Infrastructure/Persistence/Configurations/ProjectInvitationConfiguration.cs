using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class ProjectInvitationConfiguration : IEntityTypeConfiguration<ProjectInvitation>
{
    public void Configure(EntityTypeBuilder<ProjectInvitation> builder)
    {
        builder.ToTable("ProjectInvitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).IsRequired().HasMaxLength(256);
        builder.Property(i => i.Role).IsRequired();

        // 64 = độ dài SHA-256 dạng hex, khớp PasswordResetTokenConfiguration/RefreshTokenConfiguration.
        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(i => i.CreatedByIp).HasMaxLength(45);   // đủ cho IPv6

        builder.HasIndex(i => i.TokenHash).IsUnique();

        // KHÔNG unique: nhiều bản ghi Declined/Expired/Invalidated có thể tồn tại cho cùng
        // (ProjectId, Email) — tầng service tự đảm bảo chỉ một Pending tại một thời điểm.
        builder.HasIndex(i => new { i.ProjectId, i.Email });

        builder.HasOne(i => i.Project)
               .WithMany()
               .HasForeignKey(i => i.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(i => !i.Project.IsDeleted);
    }
}
