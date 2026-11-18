using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stikling.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropInvitePlainTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DatabaseStartup encrypts every token before this runs. Should one still be in plain text,
            // stop here instead of dropping it, so no open invite loses its link
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Invites WHERE Token IS NOT NULL OR TokenCiphertext IS NULL)
                    THROW 50001, 'Some invite tokens are not encrypted yet. Start the API, whose DatabaseStartup encrypts them first.', 1;
                """);

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Invites");

            migrationBuilder.AlterColumn<byte[]>(
                name: "TokenCiphertext",
                table: "Invites",
                type: "varbinary(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The column comes back empty. The tokens can't be put back in plain text without the key, and the
            // code from before EncryptInviteTokens can't find an invite without them
            migrationBuilder.AlterColumn<byte[]>(
                name: "TokenCiphertext",
                table: "Invites",
                type: "varbinary(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "Invites",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                collation: "Latin1_General_100_BIN2");
        }
    }
}
