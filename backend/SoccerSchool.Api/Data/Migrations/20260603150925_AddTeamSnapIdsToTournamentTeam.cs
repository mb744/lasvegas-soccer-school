using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamSnapIdsToTournamentTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamSnapDivisionId",
                table: "TournamentTeams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeamSnapEventId",
                table: "TournamentTeams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeamSnapParticipantId",
                table: "TournamentTeams",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamSnapDivisionId",
                table: "TournamentTeams");

            migrationBuilder.DropColumn(
                name: "TeamSnapEventId",
                table: "TournamentTeams");

            migrationBuilder.DropColumn(
                name: "TeamSnapParticipantId",
                table: "TournamentTeams");
        }
    }
}
