using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkItemTypeId",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "WorkItemTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemTypes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkItemTypeFields",
                columns: table => new
                {
                    WorkItemTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemTypeFields", x => new { x.WorkItemTypeId, x.FieldDefinitionId });
                    table.ForeignKey(
                        name: "FK_WorkItemTypeFields_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemTypeFields_WorkItemTypes_WorkItemTypeId",
                        column: x => x.WorkItemTypeId,
                        principalTable: "WorkItemTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkItemTypeId",
                table: "Tasks",
                column: "WorkItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTypeFields_FieldDefinitionId",
                table: "WorkItemTypeFields",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTypeFields_WorkItemTypeId_Order",
                table: "WorkItemTypeFields",
                columns: new[] { "WorkItemTypeId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTypes_ProjectId_Name",
                table: "WorkItemTypes",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTypes_ProjectId_Order",
                table: "WorkItemTypes",
                columns: new[] { "ProjectId", "Order" });

            // ═══════════════════════════════════════════════════════════════════════════
            // BACKFILL — bắt buộc, và phải nằm ĐÚNG ở đây: sau khi WorkItemTypes tồn tại,
            // TRƯỚC khi khoá ngoại từ Tasks được tạo.
            //
            // `Tasks.WorkItemTypeId` là cột NOT NULL và EF điền mặc định
            // '00000000-…-0000' cho mọi hàng cũ. Đó không phải một loại có thật, nên nếu
            // thêm FK trước khi vá thì migration đổ ở đúng dòng cuối cùng — trên một
            // database đã đi được nửa đường.
            //
            // 🔴 Tên/màu/icon dưới đây phải khớp TỪNG CHỮ với `WorkItemType.CreateDefault`.
            // Lệch nhau thì project cũ và project mới có loại mặc định khác nhau ngay từ
            // ngày đầu — đúng bài học đã ghi ở migration AddBoardColumns.
            // ═══════════════════════════════════════════════════════════════════════════

            migrationBuilder.Sql(@"
                INSERT INTO WorkItemTypes (Id, ProjectId, Name, Icon, Color, [Order], CreatedAt)
                SELECT NEWID(), p.Id, N'Task', 'CircleDot', '#6B7280', 0, SYSUTCDATETIME()
                FROM Projects p;
            ");

            // KHÔNG lọc IsDeleted: task đã xoá mềm vẫn là hàng thật trong bảng và vẫn phải
            // thoả khoá ngoại sắp thêm. Bỏ sót chúng là migration đổ ở bước cuối — cùng cái
            // bẫy mà AddBoardColumns đã ghi lại.
            migrationBuilder.Sql(@"
                UPDATE t
                SET t.WorkItemTypeId = wt.Id
                FROM Tasks t
                INNER JOIN WorkItemTypes wt ON wt.ProjectId = t.ProjectId;
            ");

            // Trường tuỳ biến ĐANG CÓ (ADR-059) phải gắn vào loại mặc định vừa tạo — nếu
            // không, sau migration chúng biến mất khỏi mọi task vì `BuildValuesAsync` nay
            // lọc theo loại. Dữ liệu vẫn nằm trong DB nhưng không màn nào hiện ra: đúng lớp
            // "dữ liệu mất tích" mà ADR-059 đã chặn ở chiều project.
            migrationBuilder.Sql(@"
                INSERT INTO WorkItemTypeFields (WorkItemTypeId, FieldDefinitionId, IsRequired, [Order])
                SELECT wt.Id, fd.Id, 0, fd.[Order]
                FROM WorkItemTypes wt
                INNER JOIN FieldDefinitions fd ON fd.ProjectId = wt.ProjectId;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_WorkItemTypes_WorkItemTypeId",
                table: "Tasks",
                column: "WorkItemTypeId",
                principalTable: "WorkItemTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_WorkItemTypes_WorkItemTypeId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "WorkItemTypeFields");

            migrationBuilder.DropTable(
                name: "WorkItemTypes");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_WorkItemTypeId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "WorkItemTypeId",
                table: "Tasks");
        }
    }
}
