using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHostedTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostedTournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VenueId = table.Column<int>(type: "int", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CostPerTeam = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostedTournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostedTournaments_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InvitedTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    HeadCoachName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    HeadCoachPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    HeadCoachEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    AgeGroup = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitedTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HostedTournamentTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostedTournamentId = table.Column<int>(type: "int", nullable: false),
                    LvssTeamId = table.Column<int>(type: "int", nullable: true),
                    InvitedTeamId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostedTournamentTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostedTournamentTeams_HostedTournaments_HostedTournamentId",
                        column: x => x.HostedTournamentId,
                        principalTable: "HostedTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HostedTournamentTeams_Teams_LvssTeamId",
                        column: x => x.LvssTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostedTournamentTeams_InvitedTeams_InvitedTeamId",
                        column: x => x.InvitedTeamId,
                        principalTable: "InvitedTeams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournaments_StartDate",
                table: "HostedTournaments",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournaments_VenueId",
                table: "HostedTournaments",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitedTeams_Name",
                table: "InvitedTeams",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentTeams_HostedTournamentId",
                table: "HostedTournamentTeams",
                column: "HostedTournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentTeams_LvssTeamId",
                table: "HostedTournamentTeams",
                column: "LvssTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentTeams_InvitedTeamId",
                table: "HostedTournamentTeams",
                column: "InvitedTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HostedTournamentTeams");
            migrationBuilder.DropTable(name: "HostedTournaments");
            migrationBuilder.DropTable(name: "InvitedTeams");
        }
    }
}
