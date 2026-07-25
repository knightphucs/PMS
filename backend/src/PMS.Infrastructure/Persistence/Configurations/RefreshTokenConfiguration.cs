using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TokenHash).IsRequired().HasMaxLength(64);  // SHA-256 hex = 64 ký tự
        builder.Property(rt => rt.CreatedByIp).HasMaxLength(45);             // đủ cho IPv6

        builder.HasIndex(rt => rt.TokenHash).IsUnique();
        builder.HasIndex(rt => rt.EmployeeId);

        builder.HasOne(rt => rt.Employee)
               .WithMany(e => e.RefreshTokens)
               .HasForeignKey(rt => rt.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}