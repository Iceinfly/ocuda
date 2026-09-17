using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocuda.Ops.DataProvider.SqlServer.Ops.Migrations
{
    /// <inheritdoc />
    public partial class ops_books_by_mail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BooksByMailCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Dislikes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalCustomerId = table.Column<int>(type: "int", nullable: false),
                    Likes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BooksByMailCustomers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BooksByMailCustomers_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BooksByMailCustomers_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BooksByMailComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BooksByMailCustomerId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BooksByMailComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BooksByMailComments_BooksByMailCustomers_BooksByMailCustomerId",
                        column: x => x.BooksByMailCustomerId,
                        principalTable: "BooksByMailCustomers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BooksByMailComments_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BooksByMailComments_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BooksByMailComments_BooksByMailCustomerId",
                table: "BooksByMailComments",
                column: "BooksByMailCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BooksByMailComments_CreatedBy",
                table: "BooksByMailComments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BooksByMailComments_UpdatedBy",
                table: "BooksByMailComments",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BooksByMailCustomers_CreatedBy",
                table: "BooksByMailCustomers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BooksByMailCustomers_UpdatedBy",
                table: "BooksByMailCustomers",
                column: "UpdatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BooksByMailComments");

            migrationBuilder.DropTable(
                name: "BooksByMailCustomers");
        }
    }
}
