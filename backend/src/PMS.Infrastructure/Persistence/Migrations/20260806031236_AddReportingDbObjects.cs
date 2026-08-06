using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bốn kỹ thuật DB cho hạng mục "Áp kỹ thuật DB" của báo cáo (index · view · stored
    /// procedure · trigger), cộng CHECK constraint.
    ///
    /// <para>
    /// 🔴 <b>Enum ghi cứng thành số trong SQL thô — đã xác nhận từ chính enum, không đoán:</b>
    /// <c>StatusCategory.Done = 2</c>, <c>SprintStatus.Completed = 2</c>. Đổi thứ tự khai báo
    /// hai enum này thì migration này SAI mà không có gì báo — xem ADR-052 mục "migration
    /// suýt hỏng dữ liệu im lặng" để biết chính lớp lỗi này đã từng xảy ra thật.
    /// </para>
    /// </summary>
    public partial class AddReportingDbObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_EmployeeId",
                table: "TaskAssignments");

            migrationBuilder.AddColumn<int>(
                name: "TaskCount",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // (a) INDEX — nâng cấp index FK mặc định của EF thành covering index: phục vụ
            // TallyByAssigneeAsync (group theo EmployeeId) và "Việc của tôi" mà không phải
            // lookup ngược vào bảng chính để lấy TaskId.
            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_EmployeeId",
                table: "TaskAssignments",
                column: "EmployeeId")
                .Annotation("SqlServer:Include", new[] { "TaskId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sprints_EndDate_After_StartDate",
                table: "Sprints",
                sql: "[EndDate] > [StartDate]");

            // Backfill TaskCount cho project đã có sẵn — cột mới thêm mặc định 0, và không
            // backfill thì mọi project cũ hiện sai cho tới lần task tiếp theo được tạo/xóa.
            migrationBuilder.Sql(@"
                UPDATE p
                SET p.TaskCount = ISNULL(t.Cnt, 0)
                FROM Projects p
                LEFT JOIN (
                    SELECT ProjectId, COUNT(*) AS Cnt
                    FROM Tasks
                    WHERE IsDeleted = 0
                    GROUP BY ProjectId
                ) t ON t.ProjectId = p.Id;
            ");

            // (b) VIEW — nguồn dữ liệu velocity cho nhóm báo cáo (mục 4). Chỉ tính sprint đã
            // ĐÓNG SỔ (Status = Completed = 2): velocity đo theo mốc CompletedAt, sprint chưa
            // đóng chưa có gì để đếm là "tốc độ" cả.
            migrationBuilder.Sql(@"
                CREATE VIEW vw_SprintVelocity AS
                SELECT
                    s.Id AS SprintId,
                    s.ProjectId,
                    s.Name,
                    s.CompletedAt,
                    COUNT(t.Id) AS TotalTasks,
                    SUM(CASE WHEN t.Category = 2 THEN 1 ELSE 0 END) AS DoneTasks
                FROM Sprints s
                LEFT JOIN Tasks t ON t.SprintId = s.Id AND t.IsDeleted = 0
                WHERE s.Status = 2 AND s.IsDeleted = 0
                GROUP BY s.Id, s.ProjectId, s.Name, s.CompletedAt;
            ");

            // (c) STORED PROCEDURE — tổng hợp backlog insight trong một round-trip thay vì
            // nhiều query LINQ. Tách THÀNH HAI proc (tổng quan + theo priority) vì EF Core 8
            // (Database.SqlQuery<T>) không xử lý gọn multi-resultset từ một proc duy nhất —
            // chọn thực dụng cho phạm vi một phiên thay vì vật lộn với ADO.NET reader thô.
            migrationBuilder.Sql(@"
                CREATE PROCEDURE sp_GetProjectBacklogInsight
                    @ProjectId UNIQUEIDENTIFIER,
                    @DueSoonHorizonDays INT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    DECLARE @Today DATE = CAST(SYSUTCDATETIME() AS DATE);
                    DECLARE @Horizon DATE = DATEADD(DAY, @DueSoonHorizonDays, @Today);

                    SELECT
                        COUNT(*) AS TotalOpen,
                        SUM(CASE WHEN t.DueDate IS NOT NULL
                                      AND CAST(t.DueDate AS DATE) < @Today
                                 THEN 1 ELSE 0 END) AS Overdue,
                        SUM(CASE WHEN t.DueDate IS NOT NULL
                                      AND CAST(t.DueDate AS DATE) BETWEEN @Today AND @Horizon
                                 THEN 1 ELSE 0 END) AS DueSoon,
                        SUM(CASE WHEN t.DueDate IS NULL THEN 1 ELSE 0 END) AS NoDueDate
                    FROM Tasks t
                    WHERE t.ProjectId = @ProjectId AND t.IsDeleted = 0 AND t.Category <> 2;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE PROCEDURE sp_GetProjectBacklogByPriority
                    @ProjectId UNIQUEIDENTIFIER
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT t.Priority, COUNT(*) AS Cnt
                    FROM Tasks t
                    WHERE t.ProjectId = @ProjectId AND t.IsDeleted = 0 AND t.Category <> 2
                    GROUP BY t.Priority;
                END
            ");

            // (d) TRIGGER — duy trì Projects.TaskCount. Đây là đối tượng DB kém cần thiết
            // nhất trong bốn cái: CHECK constraint ở trên mới là câu trả lời kỹ thuật đúng
            // cho "toàn vẹn dữ liệu", còn trigger này tồn tại để MINH HỌA kỹ thuật trigger
            // cho báo cáo — CountTasksAsync đã tính đúng số này tại chỗ mỗi khi cần, ứng
            // dụng không thực sự cần một cột đếm phi chuẩn hoá.
            //
            // 🔴 KHÔNG dùng `AFTER INSERT, DELETE` như bản nháp đầu: Tasks xoá MỀM
            // (ApplySoftDelete đổi DELETE thành UPDATE IsDeleted=1 trước SaveChanges), nên
            // AFTER DELETE gần như không bao giờ chạy — trigger sẽ chỉ tăng mà không bao
            // giờ giảm. Logic dưới đây gộp `inserted`/`deleted` theo IsDeleted nên xử lý
            // đúng CẢ BA trường hợp bằng một công thức: task mới (deleted rỗng, +1 mỗi hàng
            // chưa xoá), xoá mềm (IsDeleted 0→1, -1), và cập nhật không đụng IsDeleted (+1
            // rồi -1, triệt tiêu — không có gì thay đổi). AFTER DELETE vẫn giữ lại để phòng
            // xa nếu sau này có đường xoá cứng thật.
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_Tasks_MaintainProjectTaskCount
                ON Tasks
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    ;WITH Delta AS (
                        SELECT ProjectId, SUM(Change) AS Change
                        FROM (
                            SELECT ProjectId,
                                   CASE WHEN IsDeleted = 0 THEN 1 ELSE 0 END AS Change
                            FROM inserted
                            UNION ALL
                            SELECT ProjectId,
                                   CASE WHEN IsDeleted = 0 THEN -1 ELSE 0 END AS Change
                            FROM deleted
                        ) x
                        GROUP BY ProjectId
                    )
                    UPDATE p
                    SET p.TaskCount = p.TaskCount + d.Change
                    FROM Projects p
                    JOIN Delta d ON d.ProjectId = p.Id
                    WHERE d.Change <> 0;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Thứ tự NGƯỢC với Up(), và trigger phải rớt TRƯỚC khi cột TaskCount bị xóa —
            // nó đọc/ghi chính cột đó.
            migrationBuilder.Sql("DROP TRIGGER trg_Tasks_MaintainProjectTaskCount;");
            migrationBuilder.Sql("DROP PROCEDURE sp_GetProjectBacklogByPriority;");
            migrationBuilder.Sql("DROP PROCEDURE sp_GetProjectBacklogInsight;");
            migrationBuilder.Sql("DROP VIEW vw_SprintVelocity;");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_EmployeeId",
                table: "TaskAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sprints_EndDate_After_StartDate",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "TaskCount",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_EmployeeId",
                table: "TaskAssignments",
                column: "EmployeeId");
        }
    }
}
