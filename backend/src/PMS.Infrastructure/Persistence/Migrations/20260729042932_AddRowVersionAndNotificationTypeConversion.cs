using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionAndNotificationTypeConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tasks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Projects",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            // AlterColumn int -> nvarchar chỉ CAST giá trị số thành chuỗi số ("0", "1"...),
            // không thành tên enum ("TaskAssigned"...) mà HasConversion<string>() cần khi đọc
            // lại. Phải tự map để không làm hỏng dữ liệu Notification đã tồn tại.
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql("""
                UPDATE Notifications SET Type = CASE Type
                    WHEN '0' THEN 'TaskAssigned'
                    WHEN '1' THEN 'TaskUnassigned'
                    WHEN '2' THEN 'DueSoon'
                    WHEN '3' THEN 'CommentAdded'
                    WHEN '4' THEN 'StatusChanged'
                    WHEN '5' THEN 'InvitedToProject'
                    WHEN '6' THEN 'InvitationResponsed'
                    WHEN '7' THEN 'InvitationAccepted'
                    WHEN '8' THEN 'InvitationDeclined'
                    WHEN '9' THEN 'RoleChanged'
                    WHEN '10' THEN 'RemovedFromProject'
                    WHEN '11' THEN 'MemberLeftProject'
                    ELSE Type
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "LockReason",
                table: "Employees",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Projects");

            migrationBuilder.Sql("""
                UPDATE Notifications SET Type = CASE Type
                    WHEN 'TaskAssigned' THEN '0'
                    WHEN 'TaskUnassigned' THEN '1'
                    WHEN 'DueSoon' THEN '2'
                    WHEN 'CommentAdded' THEN '3'
                    WHEN 'StatusChanged' THEN '4'
                    WHEN 'InvitedToProject' THEN '5'
                    WHEN 'InvitationResponsed' THEN '6'
                    WHEN 'InvitationAccepted' THEN '7'
                    WHEN 'InvitationDeclined' THEN '8'
                    WHEN 'RoleChanged' THEN '9'
                    WHEN 'RemovedFromProject' THEN '10'
                    WHEN 'MemberLeftProject' THEN '11'
                    ELSE Type
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "Notifications",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "LockReason",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
