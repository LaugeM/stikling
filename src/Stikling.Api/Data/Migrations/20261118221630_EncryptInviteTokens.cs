using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stikling.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EncryptInviteTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invites_Token",
                table: "Invites");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "Invites",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<byte[]>(
                name: "TokenCiphertext",
                table: "Invites",
                type: "varbinary(64)",
                maxLength: 64,
                nullable: true);

            // The hash of every token there is, computed in SQL the way InviteTokens.Hash does it: SHA-256 of
            // the token's ASCII bytes as lowercase hex. Encrypting the token needs the key, which SQL doesn't
            // have, so DatabaseStartup does that on start before DropInvitePlainTokens runs.
            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "Invites",
                type: "char(64)",
                fixedLength: true,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Invites SET TokenHash = LOWER(CONVERT(char(64), HASHBYTES('SHA2_256', CAST(Token AS varchar(40))), 2))");

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "Invites",
                type: "char(64)",
                fixedLength: true,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldFixedLength: true,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invites_TokenHash",
                table: "Invites",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invites_TokenHash",
                table: "Invites");

            migrationBuilder.DropColumn(
                name: "TokenCiphertext",
                table: "Invites");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "Invites");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "Invites",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Token",
                table: "Invites",
                column: "Token",
                unique: true);
        }
    }
}
