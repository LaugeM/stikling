using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stikling.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPeopleInCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Records",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "People",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManagesPeople",
                table: "Memberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerPersonId",
                table: "Collections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FormerMembers",
                columns: table => new
                {
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeftAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormerMembers", x => new { x.CollectionId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_FormerMembers_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormerMembers_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ManagesPeople = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invites_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invites_People_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Collections_OwnerPersonId",
                table: "Collections",
                column: "OwnerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_FormerMembers_PersonId",
                table: "FormerMembers",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_CollectionId",
                table: "Invites",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_CreatedBy_CreatedAt",
                table: "Invites",
                columns: new[] { "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Token",
                table: "Invites",
                column: "Token",
                unique: true);

            // Every collection that exists gets the person who has been in it longest as an editor as
            // its owner, and the owner manages people. Until now that was the person it was made for.
            migrationBuilder.Sql("""
                UPDATE c
                SET OwnerPersonId = (
                    SELECT TOP 1 m.PersonId
                    FROM Memberships m
                    WHERE m.CollectionId = c.Id AND m.Role = 'Editor'
                    ORDER BY m.CreatedAt, m.PersonId)
                FROM Collections c
                """);

            // One with no editor, which nothing could make until now, goes to whoever has been in it longest
            migrationBuilder.Sql("""
                UPDATE c
                SET OwnerPersonId = (
                    SELECT TOP 1 m.PersonId
                    FROM Memberships m
                    WHERE m.CollectionId = c.Id
                    ORDER BY m.CreatedAt, m.PersonId)
                FROM Collections c
                WHERE c.OwnerPersonId IS NULL
                """);

            migrationBuilder.Sql("""
                UPDATE m
                SET ManagesPeople = 1, Role = 'Editor'
                FROM Memberships m
                JOIN Collections c ON c.Id = m.CollectionId AND c.OwnerPersonId = m.PersonId
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Collections_People_OwnerPersonId",
                table: "Collections",
                column: "OwnerPersonId",
                principalTable: "People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Collections_People_OwnerPersonId",
                table: "Collections");

            migrationBuilder.DropTable(
                name: "FormerMembers");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropIndex(
                name: "IX_Collections_OwnerPersonId",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Records");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "People");

            migrationBuilder.DropColumn(
                name: "ManagesPeople",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "OwnerPersonId",
                table: "Collections");
        }
    }
}
