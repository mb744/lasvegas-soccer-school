using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamCoachLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoachId",
                table: "TeamCoaches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "TeamCoaches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TeamCoaches_CoachId",
                table: "TeamCoaches",
                column: "CoachId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamCoaches_Coaches_CoachId",
                table: "TeamCoaches",
                column: "CoachId",
                principalTable: "Coaches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: best-effort link existing TeamCoach rows to a Coach in the roster.
            // Match on full name + phone first (most specific), then on full name alone. Ties
            // (multiple Coach rows with the same name) are skipped — admin can pick later.
            migrationBuilder.Sql(@"
                UPDATE tc
                SET tc.CoachId = c.Id
                FROM TeamCoaches tc
                JOIN Coaches c
                  ON LTRIM(RTRIM(tc.Name)) = LTRIM(RTRIM(c.FirstName + ' ' + c.LastName))
                 AND tc.Phone IS NOT NULL
                 AND c.CellPhone IS NOT NULL
                 AND tc.Phone = c.CellPhone
                WHERE tc.CoachId IS NULL;

                ;WITH unique_name_matches AS (
                    SELECT tc.Id AS TeamCoachId, MIN(c.Id) AS CoachId, COUNT(c.Id) AS Cnt
                    FROM TeamCoaches tc
                    JOIN Coaches c
                      ON LTRIM(RTRIM(tc.Name)) = LTRIM(RTRIM(c.FirstName + ' ' + c.LastName))
                    WHERE tc.CoachId IS NULL
                    GROUP BY tc.Id
                )
                UPDATE tc
                SET tc.CoachId = u.CoachId
                FROM TeamCoaches tc
                JOIN unique_name_matches u ON tc.Id = u.TeamCoachId
                WHERE u.Cnt = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamCoaches_Coaches_CoachId",
                table: "TeamCoaches");

            migrationBuilder.DropIndex(
                name: "IX_TeamCoaches_CoachId",
                table: "TeamCoaches");

            migrationBuilder.DropColumn(
                name: "CoachId",
                table: "TeamCoaches");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "TeamCoaches");
        }
    }
}
