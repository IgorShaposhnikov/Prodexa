using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prodexa.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plan_tasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "downtimes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EquipmentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsPlanned = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_downtimes", x => x.Id);
                    table.CheckConstraint("ck_downtimes_interval", "\"EndAt\" IS NULL OR \"EndAt\" >= \"StartAt\"");
                    table.ForeignKey(
                        name: "FK_downtimes_equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_task_revisions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TaskId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessSegmentVersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PlannedStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_task_revisions", x => x.Id);
                    table.UniqueConstraint("AK_plan_task_revisions_TaskId_Id", x => new { x.TaskId, x.Id });
                    table.CheckConstraint("ck_plan_task_revisions_period", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.CheckConstraint("ck_plan_task_revisions_quantity", "\"PlannedQuantity\" >= 0");
                    table.CheckConstraint("ck_plan_task_revisions_revision", "\"RevisionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_plan_task_revisions_plan_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "plan_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "executions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TaskId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TaskRevisionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executions", x => x.Id);
                    table.CheckConstraint("ck_executions_interval", "\"EndAt\" IS NULL OR \"EndAt\" >= \"StartAt\"");
                    table.ForeignKey(
                        name: "FK_executions_plan_task_revisions_TaskId_TaskRevisionId",
                        columns: x => new { x.TaskId, x.TaskRevisionId },
                        principalTable: "plan_task_revisions",
                        principalColumns: new[] { "TaskId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_downtimes_EquipmentId_StartAt",
                table: "downtimes",
                columns: new[] { "EquipmentId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_executions_StartAt",
                table: "executions",
                column: "StartAt");

            migrationBuilder.CreateIndex(
                name: "IX_executions_TaskId_TaskRevisionId",
                table: "executions",
                columns: new[] { "TaskId", "TaskRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_task_revisions_DueAt",
                table: "plan_task_revisions",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_plan_task_revisions_TaskId_RevisionNumber",
                table: "plan_task_revisions",
                columns: new[] { "TaskId", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "downtimes");

            migrationBuilder.DropTable(
                name: "executions");

            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "plan_task_revisions");

            migrationBuilder.DropTable(
                name: "plan_tasks");
        }
    }
}
