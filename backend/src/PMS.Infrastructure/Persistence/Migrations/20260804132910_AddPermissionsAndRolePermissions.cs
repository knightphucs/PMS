using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionsAndRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    SystemRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PermissionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.SystemRole, x.PermissionCode });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionCode",
                        column: x => x.PermissionCode,
                        principalTable: "Permissions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Code", "Description" },
                values: new object[,]
                {
                    { "audit:read", "Đọc nhật ký cấp hệ thống" },
                    { "employees:manage", "Quản lý nhân sự: xem danh sách, khóa/mở tài khoản, đổi vai trò hệ thống" },
                    { "labels:manage", "Sửa và xóa nhãn toàn cục (xóa nhãn gỡ chip khỏi board của mọi dự án)" },
                    { "projects:create", "Tạo dự án mới (người tạo tự động là Project Manager của dự án đó)" },
                    { "roles:manage", "Sửa quyền của từng vai trò hệ thống" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionCode", "SystemRole" },
                values: new object[,]
                {
                    { "audit:read", "SystemAdmin" },
                    { "employees:manage", "SystemAdmin" },
                    { "labels:manage", "SystemAdmin" },
                    { "projects:create", "SystemAdmin" },
                    { "roles:manage", "SystemAdmin" },
                    { "projects:create", "User" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionCode",
                table: "RolePermissions",
                column: "PermissionCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Permissions");
        }
    }
}
