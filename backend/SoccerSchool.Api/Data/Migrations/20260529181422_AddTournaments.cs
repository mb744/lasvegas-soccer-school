using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TournamentId",
                table: "ScheduledGames",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    GotSportEventId = table.Column<int>(type: "int", nullable: false),
                    GotSportTeamId = table.Column<int>(type: "int", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tournaments_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledGames_TournamentId",
                table: "ScheduledGames",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_TeamId",
                table: "Tournaments",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledGames_Tournaments_TournamentId",
                table: "ScheduledGames",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: the GotSport sync moved from Team to Tournament. Create one tournament for
            // every team that was GotSport-linked, then tag that team's already-scraped games
            // (ExternalUid 'gs:%'; manual games use 'manual-game-%') to the new tournament.
            migrationBuilder.Sql(@"
                INSERT INTO [Tournaments] ([Name], [TeamId], [GotSportEventId], [GotSportTeamId], [LastSyncedAt], [LastSyncMessage], [CreatedAt])
                SELECT [Name], [Id], [GotSportEventId], [GotSportTeamId], [LastSyncedAt], [LastSyncMessage], SYSUTCDATETIME()
                FROM [Teams]
                WHERE [GotSportEventId] > 0 AND [GotSportTeamId] > 0;

                UPDATE g
                SET g.[TournamentId] = tr.[Id]
                FROM [ScheduledGames] g
                INNER JOIN [Tournaments] tr ON tr.[TeamId] = g.[TeamId]
                WHERE g.[Kind] = 0
                  AND g.[TournamentId] IS NULL
                  AND g.[ExternalUid] LIKE 'gs:%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledGames_Tournaments_TournamentId",
                table: "ScheduledGames");

            migrationBuilder.DropTable(
                name: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledGames_TournamentId",
                table: "ScheduledGames");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "ScheduledGames");
        }
    }
}
