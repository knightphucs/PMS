using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");
        builder.HasKey(t => t.Id);

        // 64 = độ dài SHA-256 dạng hex, khớp RefreshTokenConfiguration.
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(t => t.CreatedByIp).HasMaxLength(45);   // đủ cho IPv6

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.EmployeeId);

        builder.HasOne(t => t.Employee)
               .WithMany()
               .HasForeignKey(t => t.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
