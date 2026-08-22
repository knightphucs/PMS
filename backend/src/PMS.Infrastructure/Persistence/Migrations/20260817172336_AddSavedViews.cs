using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SortBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SortDescending = table.Column<bool>(type: "bit", nullable: false),
                    GroupBy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViews_Employees_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedViews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SavedViewColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViewColumns", x => x.Id);
                    table.CheckConstraint("CK_SavedViewColumns_DungMotNguonTruong", "(CASE WHEN [Field]             IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [FieldDefinitionId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_SavedViewColumns_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedViewColumns_SavedViews_SavedViewId",
                        column: x => x.SavedViewId,
                        principalTable: "SavedViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedViewFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Operator = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViewFilters", x => x.Id);
                    table.CheckConstraint("CK_SavedViewFilters_DungMotNguonTruong", "(CASE WHEN [Field]             IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [FieldDefinitionId] IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.ForeignKey(
                        name: "FK_SavedViewFilters_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedViewFilters_SavedViews_SavedViewId",
                        column: x => x.SavedViewId,
                        principalTable: "SavedViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewColumns_FieldDefinitionId",
                table: "SavedViewColumns",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewColumns_SavedViewId_Order",
                table: "SavedViewColumns",
                columns: new[] { "SavedViewId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewFilters_FieldDefinitionId",
                table: "SavedViewFilters",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewFilters_SavedViewId",
                table: "SavedViewFilters",
                column: "SavedViewId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_OwnerId",
                table: "SavedViews",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_ProjectId_Order",
                table: "SavedViews",
                columns: new[] { "ProjectId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_ProjectId_OwnerId_Name",
                table: "SavedViews",
                columns: new[] { "ProjectId", "OwnerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedViewColumns");

            migrationBuilder.DropTable(
                name: "SavedViewFilters");

            migrationBuilder.DropTable(
                name: "SavedViews");
        }
    }
}
