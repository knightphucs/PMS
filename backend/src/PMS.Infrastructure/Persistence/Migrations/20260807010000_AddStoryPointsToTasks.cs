using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Thêm estimate Story Point và đưa tổng điểm Done vào nguồn dữ liệu velocity.
///
/// <para>
/// 📌 <c>[DbContext]</c> và <c>[Migration]</c> nằm ở file <c>.Designer.cs</c> đi kèm, KHÔNG
/// ở đây — đó là quy ước của <c>dotnet ef</c> và khai ở cả hai chỗ là lỗi biên dịch
/// <c>CS0579</c>. File này từng được viết TAY và thiếu hẳn Designer (2026-08-07); hệ quả là
/// <c>dotnet ef migrations remove</c> xóa trắng <c>PmsDbContextModelSnapshot</c> vì nó dựng
/// lại snapshot từ Designer của migration liền trước. Đã bổ sung 2026-08-11 (ADR-057).
/// </para>
/// </summary>
public partial class AddStoryPointsToTasks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "StoryPoints",
            table: "Tasks",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql("DROP VIEW vw_SprintVelocity;");
        migrationBuilder.Sql(@"
            CREATE VIEW vw_SprintVelocity AS
            SELECT
                s.Id AS SprintId,
                s.ProjectId,
                s.Name,
                s.CompletedAt,
                COUNT(t.Id) AS TotalTasks,
                SUM(CASE WHEN t.Category = 2 THEN 1 ELSE 0 END) AS DoneTasks,
                COALESCE(SUM(CASE WHEN t.Category = 2 THEN t.StoryPoints ELSE 0 END), 0) AS DoneStoryPoints
            FROM Sprints s
            LEFT JOIN Tasks t ON t.SprintId = s.Id AND t.IsDeleted = 0
            WHERE s.Status = 2 AND s.IsDeleted = 0
            GROUP BY s.Id, s.ProjectId, s.Name, s.CompletedAt;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP VIEW vw_SprintVelocity;");
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

        migrationBuilder.DropColumn(name: "StoryPoints", table: "Tasks");
    }
}
