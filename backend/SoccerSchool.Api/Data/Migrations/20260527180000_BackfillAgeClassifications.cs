using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <summary>
    /// One-shot backfill of <c>RegistrationPlayer.AgeClassificationId</c> for rows submitted
    /// before age classifications existed. For each player whose AgeClassificationId is null,
    /// finds the AgeClassification whose [DobStart, DobEnd] range contains the player's DOB and
    /// assigns it. If multiple ranges overlap a single DOB (shouldn't happen if admin keeps them
    /// disjoint, but defensive), picks the one with the latest DobStart — the narrowest fit when
    /// brackets nest. Rows already manually assigned are left alone.
    /// </summary>
    public partial class BackfillAgeClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE rp
                SET AgeClassificationId = (
                    SELECT TOP 1 ac.Id
                    FROM [AgeClassifications] ac
                    WHERE p.DateOfBirth >= ac.DobStart AND p.DateOfBirth <= ac.DobEnd
                    ORDER BY ac.DobStart DESC, ac.Id
                )
                FROM [RegistrationPlayers] rp
                INNER JOIN [Players] p ON p.Id = rp.PlayerId
                WHERE rp.AgeClassificationId IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible — we'd have no way to know which rows were null before the backfill
            // versus null because no bracket matched. No-op is safe; reverting just leaves the
            // assignments in place, which the app still uses correctly.
        }
    }
}
