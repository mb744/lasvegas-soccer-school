using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTiersDaysVenueFieldsAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostedTournamentTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostedTournamentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostedTournamentTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostedTournamentTiers_HostedTournaments_HostedTournamentId",
                        column: x => x.HostedTournamentId,
                        principalTable: "HostedTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HostedTournamentDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostedTournamentId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostedTournamentDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostedTournamentDays_HostedTournaments_HostedTournamentId",
                        column: x => x.HostedTournamentId,
                        principalTable: "HostedTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VenueFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueFields_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<int>(
                name: "TierId",
                table: "HostedTournamentTeams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Paid",
                table: "HostedTournamentTeams",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "HostedTournamentTeams",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "HostedTournamentTeams",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "HostedTournamentTeams",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentTeams_TierId",
                table: "HostedTournamentTeams",
                column: "TierId");

            migrationBuilder.AddForeignKey(
                name: "FK_HostedTournamentTeams_HostedTournamentTiers_TierId",
                table: "HostedTournamentTeams",
                column: "TierId",
                principalTable: "HostedTournamentTiers",
                principalColumn: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentTiers_HostedTournamentId_SortOrder",
                table: "HostedTournamentTiers",
                columns: new[] { "HostedTournamentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HostedTournamentDays_HostedTournamentId_Date",
                table: "HostedTournamentDays",
                columns: new[] { "HostedTournamentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VenueFields_VenueId_Name",
                table: "VenueFields",
                columns: new[] { "VenueId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HostedTournamentTeams_HostedTournamentTiers_TierId",
                table: "HostedTournamentTeams");

            migrationBuilder.DropIndex(
                name: "IX_HostedTournamentTeams_TierId",
                table: "HostedTournamentTeams");

            migrationBuilder.DropColumn(name: "TierId", table: "HostedTournamentTeams");
            migrationBuilder.DropColumn(name: "Paid", table: "HostedTournamentTeams");
            migrationBuilder.DropColumn(name: "PaidAt", table: "HostedTournamentTeams");
            migrationBuilder.DropColumn(name: "PaymentMethod", table: "HostedTournamentTeams");
            migrationBuilder.DropColumn(name: "PaymentReference", table: "HostedTournamentTeams");

            migrationBuilder.DropTable(name: "VenueFields");
            migrationBuilder.DropTable(name: "HostedTournamentDays");
            migrationBuilder.DropTable(name: "HostedTournamentTiers");
        }
    }
}
