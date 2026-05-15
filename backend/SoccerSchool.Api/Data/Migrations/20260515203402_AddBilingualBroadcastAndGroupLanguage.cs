using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBilingualBroadcastAndGroupLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Language",
                table: "MessageGroups",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BodyEn",
                table: "Broadcasts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyEs",
                table: "Broadcasts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            // Preserve any existing broadcast bodies by assuming they were English. Then drop the
            // old single-body column. Without this copy step the rename would silently lose data.
            migrationBuilder.Sql("UPDATE [Broadcasts] SET [BodyEn] = [Body] WHERE [Body] IS NOT NULL");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "Broadcasts");

            migrationBuilder.AddColumn<int>(
                name: "Language",
                table: "BroadcastRecipients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PhraseTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    English = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Spanish = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhraseTranslations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhraseTranslations_English",
                table: "PhraseTranslations",
                column: "English");

            migrationBuilder.CreateIndex(
                name: "IX_PhraseTranslations_Spanish",
                table: "PhraseTranslations",
                column: "Spanish");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhraseTranslations");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "MessageGroups");

            migrationBuilder.DropColumn(
                name: "BodyEn",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "BodyEs",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "BroadcastRecipients");

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "Broadcasts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }
    }
}
