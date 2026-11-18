using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stikling.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteForName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForName",
                table: "Invites",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForName",
                table: "Invites");
        }
    }
}
