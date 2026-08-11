using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldDefinitions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FieldOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldOptions_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValueNumber = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ValueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValueBoolean = table.Column<bool>(type: "bit", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldValues", x => x.Id);
                    table.CheckConstraint("CK_FieldValues_MotGiaTriDuyNhat", "(CASE WHEN ValueText    IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN ValueNumber  IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN ValueDate    IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN ValueBoolean IS NOT NULL THEN 1 ELSE 0 END) <= 1");
                    table.ForeignKey(
                        name: "FK_FieldValues_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldValues_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FieldValueOptions",
                columns: table => new
                {
                    SelectedOptionsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValuesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldValueOptions", x => new { x.SelectedOptionsId, x.ValuesId });
                    table.ForeignKey(
                        name: "FK_FieldValueOptions_FieldOptions_SelectedOptionsId",
                        column: x => x.SelectedOptionsId,
                        principalTable: "FieldOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldValueOptions_FieldValues_ValuesId",
                        column: x => x.ValuesId,
                        principalTable: "FieldValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldDefinitions_ProjectId_Label",
                table: "FieldDefinitions",
                columns: new[] { "ProjectId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldDefinitions_ProjectId_Order",
                table: "FieldDefinitions",
                columns: new[] { "ProjectId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldOptions_FieldDefinitionId_Label",
                table: "FieldOptions",
                columns: new[] { "FieldDefinitionId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldOptions_FieldDefinitionId_Order",
                table: "FieldOptions",
                columns: new[] { "FieldDefinitionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldValueOptions_ValuesId",
                table: "FieldValueOptions",
                column: "ValuesId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldValues_FieldDefinitionId",
                table: "FieldValues",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldValues_TaskId_FieldDefinitionId",
                table: "FieldValues",
                columns: new[] { "TaskId", "FieldDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldValueOptions");

            migrationBuilder.DropTable(
                name: "FieldOptions");

            migrationBuilder.DropTable(
                name: "FieldValues");

            migrationBuilder.DropTable(
                name: "FieldDefinitions");
        }
    }
}
