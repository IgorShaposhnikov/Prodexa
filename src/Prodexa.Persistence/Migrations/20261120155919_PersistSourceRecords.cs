using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prodexa.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistSourceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "source_records",
                columns: table => new
                {
                    SourceSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceRecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceRevision = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceEventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TargetId = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_records", x => new { x.SourceSystem, x.RecordType, x.SourceRecordId, x.SourceRevision });
                });

            migrationBuilder.CreateIndex(
                name: "IX_source_records_RecordType_TargetId",
                table: "source_records",
                columns: new[] { "RecordType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "source_records");
        }
    }
}
