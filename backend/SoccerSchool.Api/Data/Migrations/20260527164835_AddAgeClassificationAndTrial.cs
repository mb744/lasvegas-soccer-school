using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgeClassificationAndTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgeClassificationId",
                table: "RegistrationPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FreeTrialOver",
                table: "RegistrationPlayers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AgeClassifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DobStart = table.Column<DateOnly>(type: "date", nullable: false),
                    DobEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgeClassifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPlayers_AgeClassificationId",
                table: "RegistrationPlayers",
                column: "AgeClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_AgeClassifications_Name",
                table: "AgeClassifications",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrationPlayers_AgeClassifications_AgeClassificationId",
                table: "RegistrationPlayers",
                column: "AgeClassificationId",
                principalTable: "AgeClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegistrationPlayers_AgeClassifications_AgeClassificationId",
                table: "RegistrationPlayers");

            migrationBuilder.DropTable(
                name: "AgeClassifications");

            migrationBuilder.DropIndex(
                name: "IX_RegistrationPlayers_AgeClassificationId",
                table: "RegistrationPlayers");

            migrationBuilder.DropColumn(
                name: "AgeClassificationId",
                table: "RegistrationPlayers");

            migrationBuilder.DropColumn(
                name: "FreeTrialOver",
                table: "RegistrationPlayers");
        }
    }
}
