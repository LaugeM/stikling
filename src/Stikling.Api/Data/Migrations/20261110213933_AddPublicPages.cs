using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stikling.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicPageNames",
                columns: table => new
                {
                    NameHash = table.Column<string>(type: "char(64)", fixedLength: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicPageNames", x => x.NameHash);
                });

            migrationBuilder.CreateTable(
                name: "PublicPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TurnedOffAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Line = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Layout = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Shelves"),
                    Order = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Newest"),
                    ShowPropagations = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ShowSwap = table.Column<bool>(type: "bit", nullable: false),
                    ShowWishlist = table.Column<bool>(type: "bit", nullable: false),
                    ShowStories = table.Column<bool>(type: "bit", nullable: false),
                    Searchable = table.Column<bool>(type: "bit", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NameClaims = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicPages_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PublicPages_People_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_CollectionId",
                table: "PublicPages",
                column: "CollectionId",
                unique: true,
                filter: "[TurnedOffAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_CreatedBy_CreatedAt",
                table: "PublicPages",
                columns: new[] { "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_Name",
                table: "PublicPages",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_Token",
                table: "PublicPages",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicPageNames");

            migrationBuilder.DropTable(
                name: "PublicPages");
        }
    }
}
