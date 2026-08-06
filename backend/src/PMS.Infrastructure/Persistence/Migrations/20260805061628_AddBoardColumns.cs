using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Tasks",
                newName: "Category");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_DueDate_Status",
                table: "Tasks",
                newName: "IX_Tasks_DueDate_Category");

            migrationBuilder.AddColumn<Guid>(
                name: "BoardColumnId",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "BoardColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardColumns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_BoardColumnId",
                table: "Tasks",
                column: "BoardColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_ProjectId_Name",
                table: "BoardColumns",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_ProjectId_Order",
                table: "BoardColumns",
                columns: new[] { "ProjectId", "Order" });

            // ═══════════════════════════════════════════════════════════════════════════
            // BACKFILL (ADR-052) — bắt buộc chạy TRƯỚC khi thêm khóa ngoại bên dưới.
            //
            // 🔴 HAI cái bẫy ở đây, cái thứ hai suýt làm hỏng dữ liệu im lặng.
            //
            // (1) `RenameColumn` ở đầu migration đổi `Tasks.Status` thành `Tasks.Category`
            //     và GIỮ NGUYÊN các số đang có. Nhưng hai enum KHÔNG khớp nhau:
            //
            //         Status:         ToDo=0  InProgress=1  Review=2  Done=3
            //         StatusCategory: ToDo=0  InProgress=1  Done=2    (không có giá trị 3)
            //
            //     Nghĩa là nếu để nguyên, mọi task đang ở `Review` (2) sẽ bị đọc thành
            //     **Done**, và mọi task `Done` (3) mang một giá trị KHÔNG TỒN TẠI trong
            //     enum mới. Không có gì báo lỗi: EF cast int sang enum không kiểm miền giá
            //     trị. Hậu quả là guard "không xóa project còn task chưa xong", biểu đồ
            //     thống kê, `IsOverdue` và job nhắc hạn đều trả lời sai.
            //
            //     Thứ tự remap 2→1 TRƯỚC rồi 3→2 là bắt buộc. Làm ngược lại thì các hàng
            //     vừa được đặt thành 2 sẽ bị bước sau kéo xuống 1.
            //
            // (2) Phải gán `BoardColumnId` theo giá trị Status GỐC, tức là làm trước khi
            //     remap ở (1). Bốn cột mặc định được tạo với Order 0..3 khớp đúng thứ tự
            //     enum cũ, nên phép ghép là `Order = giá trị Status cũ`.
            //
            // Danh sách cột dưới đây phải khớp TỪNG CHỮ với `BoardColumn.CreateDefaults`.
            // Lệch nhau thì project cũ và project mới có board khác nhau ngay từ ngày đầu.
            // ═══════════════════════════════════════════════════════════════════════════

            migrationBuilder.Sql(@"
                INSERT INTO BoardColumns (Id, ProjectId, Name, Color, [Order], Category, CreatedAt)
                SELECT NEWID(), p.Id, c.Name, c.Color, c.[Order], c.Category, SYSUTCDATETIME()
                FROM Projects p
                CROSS JOIN (VALUES
                    (N'Cần làm',    '#6B7280', 0, 0),
                    (N'Đang làm',   '#3B82F6', 1, 1),
                    (N'Đang duyệt', '#A855F7', 2, 1),
                    (N'Hoàn thành', '#22C55E', 3, 2)
                ) AS c(Name, Color, [Order], Category);
            ");

            // Ghép theo Status GỐC (cột nay tên Category nhưng vẫn đang giữ số cũ).
            // KHÔNG lọc IsDeleted: task đã xóa mềm vẫn là hàng thật trong bảng và vẫn phải
            // thỏa khóa ngoại sắp thêm — bỏ sót chúng là migration đổ ở bước cuối.
            migrationBuilder.Sql(@"
                UPDATE t
                SET t.BoardColumnId = bc.Id
                FROM Tasks t
                INNER JOIN BoardColumns bc
                    ON bc.ProjectId = t.ProjectId AND bc.[Order] = t.Category;
            ");

            // Remap enum. Thứ tự hai câu này KHÔNG được đổi — xem chú thích (1) ở trên.
            migrationBuilder.Sql("UPDATE Tasks SET Category = 1 WHERE Category = 2;");
            migrationBuilder.Sql("UPDATE Tasks SET Category = 2 WHERE Category = 3;");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_BoardColumns_BoardColumnId",
                table: "Tasks",
                column: "BoardColumnId",
                principalTable: "BoardColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remap NGƯỢC trước khi bỏ bảng cột, nếu không thì `Tasks.Status` sau khi đổi
            // tên lại sẽ mang giá trị của StatusCategory chứ không phải Status — cùng lớp
            // lỗi mà `Up` phải xử lý, chỉ đổi chiều.
            //
            // CHỈ một câu: Done 2→3. ToDo(0) và InProgress(1) vốn đã trùng số ở cả hai enum.
            //
            // ⚠️ `Review` KHÔNG khôi phục được. Nó và `Đang làm` cùng thuộc nhóm InProgress
            // nên sau khi đi qua `Up` thì không còn gì phân biệt hai cột đó ở mức Category;
            // mọi task từng ở Review sẽ về `InProgress`. Thông tin nằm ở BoardColumnId, mà
            // bảng cột thì bị bỏ ngay bên dưới. Đây là mất mát MỘT CHIỀU — `Down` chỉ dùng
            // để rollback ngay sau khi lỡ chạy `Up`, không phải để đi lùi trên dữ liệu thật
            // đã sống qua vài ngày.
            migrationBuilder.Sql("UPDATE Tasks SET Category = 3 WHERE Category = 2;");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_BoardColumns_BoardColumnId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "BoardColumns");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_BoardColumnId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "BoardColumnId",
                table: "Tasks");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Tasks",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_DueDate_Category",
                table: "Tasks",
                newName: "IX_Tasks_DueDate_Status");
        }
    }
}
