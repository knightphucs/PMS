using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCodeDescriptionLabelColorAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Tasks",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Tasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "Projects",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Labels",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#6B7280");

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.CheckConstraint("CK_Attachments_ExactlyOneOwner", "([TaskId] IS NOT NULL AND [ProjectId] IS NULL) OR ([TaskId] IS NULL AND [ProjectId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Attachments_Employees_UploaderId",
                        column: x => x.UploaderId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attachments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attachments_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTaskCounters",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTaskCounters", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectTaskCounters_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ==================================================================
            // BACKFILL — sửa tay, KHÔNG do EF sinh (ADR-033).
            //
            // Bắt buộc chèn vào ĐÂY: sau khi cột đã tồn tại, TRƯỚC khi tạo hai unique
            // index bên dưới. Thứ tự EF sinh ra là ngược lại và sẽ vỡ ngay trên bất kỳ
            // database nào có sẵn dữ liệu — mọi task đều Number = 0 và mọi project đều
            // Key = '' nên index duy nhất không tạo được.
            //
            // ⚠️ KHÔNG test nào chạm tới ba khối này: PmsWebApplicationFactory chạy
            // EnsureDeleted + Migrate nên backfill luôn thao tác trên DB rỗng. Phải kiểm
            // bằng tay: `dotnet ef database update` lên DB Development đã seed.
            // ==================================================================

            // 1. Đánh số task trong từng project theo thứ tự tạo.
            //    ORDER BY CreatedAt, Id — tie-break bằng Id là BẮT BUỘC chứ không phải cho
            //    đẹp: migration AddMembershipInvariantsAndAuditFields thêm Tasks.CreatedAt
            //    với defaultValue 0001-01-01, nên mọi task tạo trước 2026-07-28 có CreatedAt
            //    y hệt nhau và ROW_NUMBER sẽ không tất định giữa các lần chạy.
            //    KHÔNG lọc IsDeleted: số của task đã xóa mềm vẫn giữ chỗ, vì mã PMS-12 đã
            //    phát tán ra comment/URL/tài liệu ngoài hệ thống.
            migrationBuilder.Sql(@"
                WITH Numbered AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY ProjectId ORDER BY CreatedAt, Id) AS rn
                    FROM Tasks
                )
                UPDATE t SET t.Number = n.rn
                FROM Tasks t INNER JOIN Numbered n ON n.Id = t.Id;");

            // 2. Sinh mã project. Dạng tất định 'PRJ<n>' thay vì bóc dấu tiếng Việt để lấy
            //    chữ cái đầu: làm việc đó trong T-SQL cần mẹo COLLATE mong manh và không
            //    review được. Mã "đẹp" chỉ sinh cho project MỚI, bằng ProjectKeyGenerator
            //    trong C# (Normalize(FormD) xử lý dấu đúng đắn). Muốn mã đẹp cho dữ liệu cũ
            //    thì UPDATE tay sau khi đã nhìn tên thật — không nhét vào migration.
            migrationBuilder.Sql(@"
                WITH Numbered AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS rn FROM Projects
                )
                UPDATE p SET p.[Key] = 'PRJ' + CAST(n.rn AS nvarchar(6))
                FROM Projects p INNER JOIN Numbered n ON n.Id = p.Id;");

            // 3. Khởi tạo bộ đếm = số task cao nhất hiện có. Sai bước này thì task tạo đầu
            //    tiên sau migration sẽ nhận số 1 và đụng unique index vừa tạo.
            migrationBuilder.Sql(@"
                INSERT INTO ProjectTaskCounters (ProjectId, NextNumber)
                SELECT p.Id, ISNULL((SELECT MAX(t.Number) FROM Tasks t WHERE t.ProjectId = p.Id), 0)
                FROM Projects p;");

            // ================== hết BACKFILL ==================

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Number",
                table: "Tasks",
                columns: new[] { "ProjectId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Key",
                table: "Projects",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ProjectId",
                table: "Attachments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_StoredFileName",
                table: "Attachments",
                column: "StoredFileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TaskId",
                table: "Attachments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_UploaderId",
                table: "Attachments",
                column: "UploaderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "ProjectTaskCounters");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Number",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Key",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "Labels");
        }
    }
}
