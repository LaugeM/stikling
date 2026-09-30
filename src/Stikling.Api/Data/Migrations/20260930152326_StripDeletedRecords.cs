using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stikling.Api.Data.Migrations
{
    /// <summary>
    /// Deleted records are now stripped when the deletion arrives (<c>SyncRules.AsStored</c>).
    /// This strips the ones that arrived before, the same way. Their change numbers stay as they
    /// are, since devices already have the deletion.
    /// </summary>
    public partial class StripDeletedRecords : Migration
    {
        // JSON_MODIFY leaves a field out when its value is NULL, as AsStored does
        public const string Strip = """
                UPDATE Records SET Data =
                    JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY('{}',
                        '$.id', JSON_VALUE(Data, '$.id')),
                        '$.createdAt', JSON_VALUE(Data, '$.createdAt')),
                        '$.updatedAt', JSON_VALUE(Data, '$.updatedAt')),
                        '$.deletedAt', JSON_VALUE(Data, '$.deletedAt'))
                WHERE DeletedAt IS NOT NULL AND JSON_VALUE(Data, '$.mergedIntoId') IS NULL
                """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Strip);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // What was stripped is gone
        }
    }
}
