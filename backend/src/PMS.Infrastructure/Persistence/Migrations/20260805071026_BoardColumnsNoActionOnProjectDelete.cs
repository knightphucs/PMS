using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BoardColumnsNoActionOnProjectDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardColumns_Projects_ProjectId",
                table: "BoardColumns");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardColumns_Projects_ProjectId",
                table: "BoardColumns",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardColumns_Projects_ProjectId",
                table: "BoardColumns");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardColumns_Projects_ProjectId",
                table: "BoardColumns",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
