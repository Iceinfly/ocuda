using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocuda.Ops.DataProvider.SqlServer.Ops.Migrations
{
    /// <inheritdoc />
    public partial class ops_v100358 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmediaStats",
                columns: table => new
                {
                    EmediaId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Accesses = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmediaStats", x => new { x.Month, x.Year, x.EmediaId });
                });

            migrationBuilder.CreateTable(
                name: "PermissionGroupReportings",
                columns: table => new
                {
                    PermissionGroupId = table.Column<int>(type: "int", nullable: false),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    CanImport = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionGroupReportings", x => new { x.PermissionGroupId, x.ReportId });
                });

            migrationBuilder.CreateTable(
                name: "RenewCardStats",
                columns: table => new
                {
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Accepted = table.Column<int>(type: "int", nullable: false),
                    Denied = table.Column<int>(type: "int", nullable: false),
                    Discarded = table.Column<int>(type: "int", nullable: false),
                    Partial = table.Column<int>(type: "int", nullable: false),
                    Unprocessed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewCardStats", x => new { x.Month, x.Year });
                });

            migrationBuilder.CreateTable(
                name: "ReportingLocationSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    Sha256Checksum = table.Column<byte[]>(type: "varbinary(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingLocationSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportingLocationSets_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportingLocationSets_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportingImportHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Filename = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Month = table.Column<int>(type: "int", nullable: false),
                    ReportingLocationSetId = table.Column<int>(type: "int", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Total = table.Column<int>(type: "int", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingImportHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportingImportHeaders_ReportingLocationSets_ReportingLocationSetId",
                        column: x => x.ReportingLocationSetId,
                        principalTable: "ReportingLocationSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportingImportHeaders_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportingImportHeaders_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportingLocations",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    ReportingLocationSetId = table.Column<int>(type: "int", nullable: false),
                    Abbreviation = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FallbackLocation = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingLocations", x => new { x.LocationId, x.ReportingLocationSetId });
                    table.ForeignKey(
                        name: "FK_ReportingLocations_ReportingLocationSets_ReportingLocationSetId",
                        column: x => x.ReportingLocationSetId,
                        principalTable: "ReportingLocationSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportingImportData",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    ReportingImportHeaderId = table.Column<int>(type: "int", nullable: false),
                    ReportValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingImportData", x => new { x.ReportingImportHeaderId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_ReportingImportData_ReportingImportHeaders_ReportingImportHeaderId",
                        column: x => x.ReportingImportHeaderId,
                        principalTable: "ReportingImportHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportingImportDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ReportingImportHeaderId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingImportDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportingImportDetails_ReportingImportHeaders_ReportingImportHeaderId",
                        column: x => x.ReportingImportHeaderId,
                        principalTable: "ReportingImportHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportingImportDetails_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportingImportDetails_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportingImportDetails_CreatedBy",
                table: "ReportingImportDetails",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingImportDetails_ReportingImportHeaderId",
                table: "ReportingImportDetails",
                column: "ReportingImportHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingImportDetails_UpdatedBy",
                table: "ReportingImportDetails",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingImportHeaders_CreatedBy",
                table: "ReportingImportHeaders",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingImportHeaders_ReportingLocationSetId",
                table: "ReportingImportHeaders",
                column: "ReportingLocationSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingImportHeaders_UpdatedBy",
                table: "ReportingImportHeaders",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingLocations_ReportingLocationSetId",
                table: "ReportingLocations",
                column: "ReportingLocationSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingLocationSets_CreatedBy",
                table: "ReportingLocationSets",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReportingLocationSets_UpdatedBy",
                table: "ReportingLocationSets",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmediaStats");

            migrationBuilder.DropTable(
                name: "PermissionGroupReportings");

            migrationBuilder.DropTable(
                name: "RenewCardStats");

            migrationBuilder.DropTable(
                name: "ReportingImportData");

            migrationBuilder.DropTable(
                name: "ReportingImportDetails");

            migrationBuilder.DropTable(
                name: "ReportingLocations");

            migrationBuilder.DropTable(
                name: "ReportingImportHeaders");

            migrationBuilder.DropTable(
                name: "ReportingLocationSets");
        }
    }
}
