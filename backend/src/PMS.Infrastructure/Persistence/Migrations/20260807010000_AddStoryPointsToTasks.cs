using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PMS.Infrastructure.Persistence;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations;

/// <summary>Thêm estimate Story Point và đưa tổng điểm Done vào nguồn dữ liệu velocity.</summary>
[DbContext(typeof(PmsDbContext))]
[Migration("20260807010000_AddStoryPointsToTasks")]
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
