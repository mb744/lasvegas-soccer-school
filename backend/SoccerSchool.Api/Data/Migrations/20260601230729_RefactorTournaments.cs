using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Teams_TeamId",
                table: "Tournaments");

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "Tournaments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerPlayer",
                table: "Tournaments",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Tournaments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Tournaments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "Tournaments",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TournamentId",
                table: "Broadcasts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TournamentAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentAttendances_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TournamentAttendances_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_TournamentId",
                table: "Broadcasts",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentAttendances_PlayerId",
                table: "TournamentAttendances",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentAttendances_TournamentId_PlayerId",
                table: "TournamentAttendances",
                columns: new[] { "TournamentId", "PlayerId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Broadcasts_Tournaments_TournamentId",
                table: "Broadcasts",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Teams_TeamId",
                table: "Tournaments",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Broadcasts_Tournaments_TournamentId",
                table: "Broadcasts");

            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Teams_TeamId",
                table: "Tournaments");

            migrationBuilder.DropTable(
                name: "TournamentAttendances");

            migrationBuilder.DropIndex(
                name: "IX_Broadcasts_TournamentId",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "CostPerPlayer",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "Broadcasts");

            migrationBuilder.AlterColumn<int>(
                name: "TeamId",
                table: "Tournaments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Teams_TeamId",
                table: "Tournaments",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
