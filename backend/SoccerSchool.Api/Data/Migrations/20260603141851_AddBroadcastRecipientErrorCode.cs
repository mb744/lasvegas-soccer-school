using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastRecipientErrorCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "BroadcastRecipients",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            // Backfill ErrorCode from the existing StatusMessage column, which webhook callbacks
            // formatted as "status: failed (error <code>: <msg>)". Extract whatever lives between
            // "(error " and the next ":" so any code (not just 131049) gets carried over.
            migrationBuilder.Sql(@"
                UPDATE BroadcastRecipients
                SET ErrorCode = SUBSTRING(
                    StatusMessage,
                    CHARINDEX('(error ', StatusMessage) + 7,
                    CHARINDEX(':', StatusMessage, CHARINDEX('(error ', StatusMessage) + 7)
                        - (CHARINDEX('(error ', StatusMessage) + 7))
                WHERE StatusMessage LIKE '%(error %:%)%'
                  AND CHARINDEX('(error ', StatusMessage) > 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "BroadcastRecipients");
        }
    }
}
