using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBracketsFieldsMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "HostedTournaments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RulesOfPlay",
                table: "HostedTournaments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchDurationMinutes",
                table: "HostedTournaments",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "CrossBracketPlay",
                table: "HostedTournamentTiers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BracketId",
                table: "HostedTournamentTeams",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HostedTournamentBrackets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TierId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostedTournamentBrackets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostedTournamentBrackets_HostedTournamentTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "HostedTournamentTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HostedTournamentFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostedTournamentId = table.Column<int>(type: "int", nullable: false),
                    VenueFieldId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostedTournamentFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostedTournamentFields_HostedTournaments_HostedTournamentId",
                        column: x => x.HostedTournamentId,
                        principalTable: "HostedTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HostedTournamentFields_VenueFields_VenueFieldId",
                        column: x => x.VenueFieldId,
                        principalTable: "VenueFields",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HostedTournamentMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostedTournamentId = table.Column<int>(type: "int", nullable: false),
                    TierId = table.Column<int>(type: "int", nullable: true),
                    TeamAId = table.Column<int>(type: "int", nullable: true),
                    TeamBId = table.Column<int>(type: "int", nullable: true),
                    FieldId = table.Column<int>(type: "int", nullable: true),
                    DayId = table.Column<int>(type: "int", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostedTournamentMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostedTournamentMatches_HostedTournaments_HostedTournamentId",
                        column: x => x.HostedTournamentId,
                        principalTable: "HostedTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HostedTournamentMatches_HostedTournamentTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "HostedTournamentTiers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostedTournamentMatches_HostedTournamentTeams_TeamAId",
                        column: x => x.TeamAId,
                        principalTable: "HostedTournamentTeams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostedTournamentMatches_HostedTournamentTeams_TeamBId",
                        column: x => x.TeamBId,
                        principalTable: "HostedTournamentTeams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostedTournamentMatches_HostedTournamentFields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "HostedTournamentFields",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HostedTournamentMatches_HostedTournamentDays_DayId",
                        column: x => x.DayId,
                        principalTable: "HostedTournamentDays",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentBrackets_TierId_SortOrder",
                table: "HostedTournamentBrackets",
                columns: new[] { "TierId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentFields_HostedTournamentId",
                table: "HostedTournamentFields",
                column: "HostedTournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentFields_VenueFieldId",
                table: "HostedTournamentFields",
                column: "VenueFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentMatches_HostedTournamentId_DayId_StartTime",
                table: "HostedTournamentMatches",
                columns: new[] { "HostedTournamentId", "DayId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentMatches_TierId",
                table: "HostedTournamentMatches",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentMatches_TeamAId",
                table: "HostedTournamentMatches",
                column: "TeamAId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentMatches_TeamBId",
                table: "HostedTournamentMatches",
                column: "TeamBId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentMatches_FieldId",
                table: "HostedTournamentMatches",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentMatches_DayId",
                table: "HostedTournamentMatches",
                column: "DayId");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentTeams_BracketId",
                table: "HostedTournamentTeams",
                column: "BracketId");

            migrationBuilder.AddForeignKey(
                name: "FK_HostedTournamentTeams_HostedTournamentBrackets_BracketId",
                table: "HostedTournamentTeams",
                column: "BracketId",
                principalTable: "HostedTournamentBrackets",
                principalColumn: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournaments_PublicSlug",
                table: "HostedTournaments",
                column: "PublicSlug",
                unique: true,
                filter: "[PublicSlug] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_HostedTournaments_PublicSlug", table: "HostedTournaments");
            migrationBuilder.DropForeignKey(name: "FK_HostedTournamentTeams_HostedTournamentBrackets_BracketId", table: "HostedTournamentTeams");
            migrationBuilder.DropIndex(name: "IX_HostedTournamentTeams_BracketId", table: "HostedTournamentTeams");
            migrationBuilder.DropTable(name: "HostedTournamentMatches");
            migrationBuilder.DropTable(name: "HostedTournamentFields");
            migrationBuilder.DropTable(name: "HostedTournamentBrackets");
            migrationBuilder.DropColumn(name: "BracketId", table: "HostedTournamentTeams");
            migrationBuilder.DropColumn(name: "CrossBracketPlay", table: "HostedTournamentTiers");
            migrationBuilder.DropColumn(name: "MatchDurationMinutes", table: "HostedTournaments");
            migrationBuilder.DropColumn(name: "RulesOfPlay", table: "HostedTournaments");
            migrationBuilder.DropColumn(name: "PublicSlug", table: "HostedTournaments");
        }
    }
}
