using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "ScheduledGames",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "ScheduledGames",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledGameId",
                table: "Broadcasts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_ScheduledGameId",
                table: "Broadcasts",
                column: "ScheduledGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_Broadcasts_ScheduledGames_ScheduledGameId",
                table: "Broadcasts",
                column: "ScheduledGameId",
                principalTable: "ScheduledGames",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Broadcasts_ScheduledGames_ScheduledGameId",
                table: "Broadcasts");

            migrationBuilder.DropIndex(
                name: "IX_Broadcasts_ScheduledGameId",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "ScheduledGames");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "ScheduledGames");

            migrationBuilder.DropColumn(
                name: "ScheduledGameId",
                table: "Broadcasts");
        }
    }
}
