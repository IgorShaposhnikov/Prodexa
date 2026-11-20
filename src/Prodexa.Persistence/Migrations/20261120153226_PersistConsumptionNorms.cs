using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prodexa.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistConsumptionNorms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_definition_versions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaterialClassId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseUnitId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceRecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_definition_versions", x => x.Id);
                    table.CheckConstraint("ck_material_definition_versions_period", "\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\"");
                    table.CheckConstraint("ck_material_definition_versions_revision", "\"VersionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_material_definition_versions_material_definitions_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consumption_norms",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductVersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialVersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcessSegmentVersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QuantityPerOutput = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    InputUnitId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OutputUnitId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumption_norms", x => x.Id);
                    table.CheckConstraint("ck_consumption_norms_period", "\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\"");
                    table.CheckConstraint("ck_consumption_norms_quantity", "\"QuantityPerOutput\" > 0");
                    table.CheckConstraint("ck_consumption_norms_revision", "\"VersionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_consumption_norms_material_definition_versions_MaterialVers~",
                        column: x => x.MaterialVersionId,
                        principalTable: "material_definition_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consumption_norms_material_definition_versions_ProductVersi~",
                        column: x => x.ProductVersionId,
                        principalTable: "material_definition_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_task_norms",
                columns: table => new
                {
                    TaskRevisionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormVersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_task_norms", x => new { x.TaskRevisionId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_plan_task_norms_consumption_norms_NormVersionId",
                        column: x => x.NormVersionId,
                        principalTable: "consumption_norms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_task_norms_material_definitions_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_task_norms_plan_task_revisions_TaskRevisionId",
                        column: x => x.TaskRevisionId,
                        principalTable: "plan_task_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consumption_norms_MaterialVersionId",
                table: "consumption_norms",
                column: "MaterialVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_consumption_norms_NormId_VersionNumber",
                table: "consumption_norms",
                columns: new[] { "NormId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consumption_norms_ProductVersionId",
                table: "consumption_norms",
                column: "ProductVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_material_definition_versions_MaterialId_VersionNumber",
                table: "material_definition_versions",
                columns: new[] { "MaterialId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_task_norms_MaterialId",
                table: "plan_task_norms",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_task_norms_NormVersionId",
                table: "plan_task_norms",
                column: "NormVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_task_norms");

            migrationBuilder.DropTable(
                name: "consumption_norms");

            migrationBuilder.DropTable(
                name: "material_definition_versions");
        }
    }
}
