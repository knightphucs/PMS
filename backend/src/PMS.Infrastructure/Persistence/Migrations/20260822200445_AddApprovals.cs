using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetColumnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MinApprovals = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalPolicies", x => x.Id);
                    table.CheckConstraint("CK_ApprovalPolicies_MinApprovalsDuong", "[MinApprovals] >= 1");
                    table.ForeignKey(
                        name: "FK_ApprovalPolicies_BoardColumns_TargetColumnId",
                        column: x => x.TargetColumnId,
                        principalTable: "BoardColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalPolicies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApprovalPolicies_WorkItemTypes_WorkItemTypeId",
                        column: x => x.WorkItemTypeId,
                        principalTable: "WorkItemTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalPolicyApprovers",
                columns: table => new
                {
                    ApprovalPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalPolicyApprovers", x => new { x.ApprovalPolicyId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_ApprovalPolicyApprovers_ApprovalPolicies_ApprovalPolicyId",
                        column: x => x.ApprovalPolicyId,
                        principalTable: "ApprovalPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalPolicyApprovers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Approvals_ApprovalPolicies_ApprovalPolicyId",
                        column: x => x.ApprovalPolicyId,
                        principalTable: "ApprovalPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Approvals_Employees_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Approvals_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_Approvals_ApprovalId",
                        column: x => x.ApprovalId,
                        principalTable: "Approvals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalDecisions_Employees_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_ApprovalId_ApproverId",
                table: "ApprovalDecisions",
                columns: new[] { "ApprovalId", "ApproverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_ApproverId",
                table: "ApprovalDecisions",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_ProjectId_WorkItemTypeId_TargetColumnId",
                table: "ApprovalPolicies",
                columns: new[] { "ProjectId", "WorkItemTypeId", "TargetColumnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_TargetColumnId",
                table: "ApprovalPolicies",
                column: "TargetColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_WorkItemTypeId",
                table: "ApprovalPolicies",
                column: "WorkItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicyApprovers_EmployeeId",
                table: "ApprovalPolicyApprovers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_ApprovalPolicyId",
                table: "Approvals",
                column: "ApprovalPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_RequestedById",
                table: "Approvals",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_TaskId_ApprovalPolicyId_ConsumedAt",
                table: "Approvals",
                columns: new[] { "TaskId", "ApprovalPolicyId", "ConsumedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalDecisions");

            migrationBuilder.DropTable(
                name: "ApprovalPolicyApprovers");

            migrationBuilder.DropTable(
                name: "Approvals");

            migrationBuilder.DropTable(
                name: "ApprovalPolicies");
        }
    }
}
