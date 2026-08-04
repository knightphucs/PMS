using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Application.Common.Authorization;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        // Khóa ghép CHÍNH LÀ ràng buộc duy nhất — cấp trùng là bất khả thi về mặt vật lý.
        // Đường đọc nóng duy nhất là `WHERE SystemRole = @role`, đúng cột dẫn đầu của khóa
        // clustered này, nên không cần thêm index nào.
        builder.HasKey(rp => new { rp.SystemRole, rp.PermissionCode });

        // Lưu tên enum thay vì số — tiền lệ ActivityLogConfiguration.Action.
        // ⚠️ Employees.SystemRole đang lưu dạng int, tức một enum có hai định dạng lưu trữ
        // trong cùng một database. Vô hại vì KHÔNG bao giờ có JOIN giữa hai cột đó (cả hai
        // truy vấn đều lọc theo hằng độc lập, EF dịch đúng converter của từng cột). Chọn
        // chuỗi ở đây vì giá trị của bảng này là đọc được bằng mắt lúc điều tra sự cố.
        builder.Property(rp => rp.SystemRole)
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(rp => rp.PermissionCode).HasMaxLength(64);

        // Restrict chứ không Cascade: danh mục là ĐÓNG nên xóa một Permission là bug theo
        // định nghĩa. Restrict biến nó thành lỗi khóa ngoại ầm ĩ, thay vì âm thầm thu hồi
        // quyền của mọi vai trò.
        builder.HasOne(rp => rp.Permission)
               .WithMany(p => p.RolePermissions)
               .HasForeignKey(rp => rp.PermissionCode)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new RolePermission { SystemRole = SystemRole.SystemAdmin, PermissionCode = SystemPermissions.EmployeesManage },
            new RolePermission { SystemRole = SystemRole.SystemAdmin, PermissionCode = SystemPermissions.AuditRead },
            new RolePermission { SystemRole = SystemRole.SystemAdmin, PermissionCode = SystemPermissions.LabelsManage },
            new RolePermission { SystemRole = SystemRole.SystemAdmin, PermissionCode = SystemPermissions.RolesManage },
            new RolePermission { SystemRole = SystemRole.SystemAdmin, PermissionCode = SystemPermissions.ProjectsCreate },

            // 🔴 User PHẢI có projects:create. Policy `can-create-project` cũ là no-op
            // (RequireAuthenticatedUser) nên MỌI người dùng vẫn tạo được project — §10 nói rõ
            // "Mọi User đều có quyền tạo Project mới". Bỏ dòng này là đổi hành vi sản phẩm,
            // và làm đỏ hàng chục test tích hợp không liên quan gì tới quyền (gần như test
            // class nào cũng gọi IntegrationTestBase.CreateProjectAsync bằng user thường).
            new RolePermission { SystemRole = SystemRole.User, PermissionCode = SystemPermissions.ProjectsCreate });
    }
}
