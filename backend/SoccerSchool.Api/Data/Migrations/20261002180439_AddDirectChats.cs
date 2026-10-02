using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DirectKey",
                table: "ChatGroups",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDirect",
                table: "ChatGroups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroups_DirectKey",
                table: "ChatGroups",
                column: "DirectKey",
                unique: true,
                filter: "[DirectKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatGroups_DirectKey",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "DirectKey",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "IsDirect",
                table: "ChatGroups");
        }
    }
}
