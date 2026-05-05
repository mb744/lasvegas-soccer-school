using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniformSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShirtSize",
                table: "RegistrationPlayers");

            migrationBuilder.RenameColumn(
                name: "ShortSize",
                table: "RegistrationPlayers",
                newName: "UniformSize");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UniformSize",
                table: "RegistrationPlayers",
                newName: "ShortSize");

            migrationBuilder.AddColumn<string>(
                name: "ShirtSize",
                table: "RegistrationPlayers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }
    }
}
