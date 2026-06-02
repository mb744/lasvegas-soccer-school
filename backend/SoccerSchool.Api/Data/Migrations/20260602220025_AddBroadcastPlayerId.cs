using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastPlayerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Broadcasts_TournamentId",
                table: "Broadcasts");

            migrationBuilder.AddColumn<int>(
                name: "PlayerId",
                table: "Broadcasts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_PlayerId",
                table: "Broadcasts",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_TournamentId_PlayerId_CreatedAt",
                table: "Broadcasts",
                columns: new[] { "TournamentId", "PlayerId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Broadcasts_Players_PlayerId",
                table: "Broadcasts",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Broadcasts_Players_PlayerId",
                table: "Broadcasts");

            migrationBuilder.DropIndex(
                name: "IX_Broadcasts_PlayerId",
                table: "Broadcasts");

            migrationBuilder.DropIndex(
                name: "IX_Broadcasts_TournamentId_PlayerId_CreatedAt",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "Broadcasts");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_TournamentId",
                table: "Broadcasts",
                column: "TournamentId");
        }
    }
}
