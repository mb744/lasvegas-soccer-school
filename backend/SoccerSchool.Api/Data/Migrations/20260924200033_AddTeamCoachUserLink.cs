using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamCoachUserLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "TeamCoaches",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamCoaches_UserId",
                table: "TeamCoaches",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamCoaches_AspNetUsers_UserId",
                table: "TeamCoaches",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamCoaches_AspNetUsers_UserId",
                table: "TeamCoaches");

            migrationBuilder.DropIndex(
                name: "IX_TeamCoaches_UserId",
                table: "TeamCoaches");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TeamCoaches");
        }
    }
}
