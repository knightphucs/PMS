using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRequestable",
                table: "WorkItemTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequestInstructions",
                table: "WorkItemTypes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTypes_IsRequestable",
                table: "WorkItemTypes",
                column: "IsRequestable",
                filter: "[IsRequestable] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemTypes_IsRequestable",
                table: "WorkItemTypes");

            migrationBuilder.DropColumn(
                name: "IsRequestable",
                table: "WorkItemTypes");

            migrationBuilder.DropColumn(
                name: "RequestInstructions",
                table: "WorkItemTypes");
        }
    }
}
