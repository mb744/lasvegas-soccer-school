using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    GotSportEventId = table.Column<int>(type: "int", nullable: false),
                    GotSportTeamId = table.Column<int>(type: "int", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentTeams_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TeamId",
                table: "TournamentTeams",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentTeams_TournamentId_TeamId",
                table: "TournamentTeams",
                columns: new[] { "TournamentId", "TeamId" },
                unique: true);

            // Backfill: every existing tournament with a TeamId becomes one TournamentTeam row
            // (carrying the tournament's GotSport IDs + last-sync state). Lets the new multi-team
            // UI surface legacy tournaments without admin re-entry. Idempotent — won't duplicate
            // if a row already exists for the pair.
            migrationBuilder.Sql(@"
                INSERT INTO TournamentTeams
                    (TournamentId, TeamId, GotSportEventId, GotSportTeamId, LastSyncedAt, LastSyncMessage, CreatedAt)
                SELECT t.Id, t.TeamId, t.GotSportEventId, t.GotSportTeamId, t.LastSyncedAt, t.LastSyncMessage, SYSUTCDATETIME()
                FROM Tournaments t
                WHERE t.TeamId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM TournamentTeams tt
                      WHERE tt.TournamentId = t.Id AND tt.TeamId = t.TeamId
                  );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentTeams");
        }
    }
}
