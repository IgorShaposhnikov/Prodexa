using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prodexa.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistMaterialEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_definitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "material_lots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InitialQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    UnitId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_lots", x => x.Id);
                    table.CheckConstraint("ck_material_lots_initial_quantity", "\"InitialQuantity\" IS NULL OR \"InitialQuantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_material_lots_material_definitions_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "material_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_movements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExecutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialLotId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OutputQuality = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReversesMovementId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_movements", x => x.Id);
                    table.CheckConstraint("ck_material_movements_quality", "(\"Kind\" = 'Output' AND \"OutputQuality\" IS NOT NULL) OR (\"Kind\" <> 'Output' AND \"OutputQuality\" IS NULL)");
                    table.CheckConstraint("ck_material_movements_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_material_movements_executions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_movements_material_lots_MaterialLotId",
                        column: x => x.MaterialLotId,
                        principalTable: "material_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_material_movements_material_movements_ReversesMovementId",
                        column: x => x.ReversesMovementId,
                        principalTable: "material_movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_material_lots_MaterialId",
                table: "material_lots",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_material_movements_ExecutionId_OccurredAt",
                table: "material_movements",
                columns: new[] { "ExecutionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_material_movements_Kind_OutputQuality_OccurredAt",
                table: "material_movements",
                columns: new[] { "Kind", "OutputQuality", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_material_movements_MaterialLotId",
                table: "material_movements",
                column: "MaterialLotId");

            migrationBuilder.CreateIndex(
                name: "IX_material_movements_ReversesMovementId",
                table: "material_movements",
                column: "ReversesMovementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "material_movements");

            migrationBuilder.DropTable(
                name: "material_lots");

            migrationBuilder.DropTable(
                name: "material_definitions");
        }
    }
}
