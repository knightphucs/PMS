using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Email).IsRequired().HasMaxLength(256);
        builder.Property(e => e.PasswordHash).IsRequired().HasMaxLength(256);
        builder.Property(e => e.SystemRole).IsRequired();
        builder.Property(e => e.LockReason).HasMaxLength(256);

        builder.HasIndex(e => e.Email).IsUnique();
    }
}