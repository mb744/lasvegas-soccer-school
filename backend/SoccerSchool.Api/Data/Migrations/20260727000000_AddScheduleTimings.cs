using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleTimings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HalfMinutes",
                table: "HostedTournaments",
                type: "int",
                nullable: false,
                defaultValue: 25);

            migrationBuilder.AddColumn<int>(
                name: "HalftimeMinutes",
                table: "HostedTournaments",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "MinutesBetweenGames",
                table: "HostedTournaments",
                type: "int",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MinutesBetweenGames", table: "HostedTournaments");
            migrationBuilder.DropColumn(name: "HalftimeMinutes", table: "HostedTournaments");
            migrationBuilder.DropColumn(name: "HalfMinutes", table: "HostedTournaments");
        }
    }
}
