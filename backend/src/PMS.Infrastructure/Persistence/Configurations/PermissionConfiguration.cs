using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Application.Common.Authorization;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        // Khóa TỰ NHIÊN — lý do đầy đủ ở XML doc của Permission.
        builder.HasKey(p => p.Code);

        builder.Property(p => p.Code).HasMaxLength(64);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(200);

        // 🔴 Seed bằng HasData, TUYỆT ĐỐI không phải DbSeeder — ba lý do độc lập:
        //   1. PmsWebApplicationFactory chỉ chạy EnsureDeleted + Migrate, KHÔNG gọi DbSeeder.
        //   2. DbSeeder chỉ chạy trong nhánh IsDevelopment(), còn test dùng env "Testing".
        //   3. DbSeeder early-return khi DB đã có Employee.
        // Không có hàng permission thì mọi policy trả 403 và cả suite tích hợp đỏ — trong đó
        // có hàng chục test chẳng liên quan gì tới quyền, chỉ vì chúng gọi CreateProjectAsync.
        //
        // Hệ quả phải nhớ: hàng permission nay là SCHEMA, không phải data. Thêm quyền =
        // const trong SystemPermissions -> HasData ở đây -> `dotnet ef migrations add`.
        builder.HasData(
            new Permission
            {
                Code = SystemPermissions.EmployeesManage,
                Description = "Quản lý nhân sự: xem danh sách, khóa/mở tài khoản, đổi vai trò hệ thống"
            },
            new Permission
            {
                Code = SystemPermissions.AuditRead,
                Description = "Đọc nhật ký cấp hệ thống"
            },
            new Permission
            {
                Code = SystemPermissions.LabelsManage,
                Description = "Sửa và xóa nhãn toàn cục (xóa nhãn gỡ chip khỏi board của mọi dự án)"
            },
            new Permission
            {
                Code = SystemPermissions.ProjectsCreate,
                Description = "Tạo dự án mới (người tạo tự động là Project Manager của dự án đó)"
            },
            new Permission
            {
                Code = SystemPermissions.RolesManage,
                Description = "Sửa quyền của từng vai trò hệ thống"
            });
    }
}
