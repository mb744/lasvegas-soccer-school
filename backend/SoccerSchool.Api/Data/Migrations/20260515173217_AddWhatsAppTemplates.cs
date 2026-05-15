using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateVariablesJson",
                table: "Broadcasts",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WhatsAppTemplateId",
                table: "Broadcasts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContentSid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PreviewText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplateVariables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WhatsAppTemplateId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Example = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplateVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppTemplateVariables_WhatsAppTemplates_WhatsAppTemplateId",
                        column: x => x.WhatsAppTemplateId,
                        principalTable: "WhatsAppTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_WhatsAppTemplateId",
                table: "Broadcasts",
                column: "WhatsAppTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_ContentSid",
                table: "WhatsAppTemplates",
                column: "ContentSid");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_Name",
                table: "WhatsAppTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplateVariables_WhatsAppTemplateId_Position",
                table: "WhatsAppTemplateVariables",
                columns: new[] { "WhatsAppTemplateId", "Position" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Broadcasts_WhatsAppTemplates_WhatsAppTemplateId",
                table: "Broadcasts",
                column: "WhatsAppTemplateId",
                principalTable: "WhatsAppTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Broadcasts_WhatsAppTemplates_WhatsAppTemplateId",
                table: "Broadcasts");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplateVariables");

            migrationBuilder.DropTable(
                name: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Broadcasts_WhatsAppTemplateId",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "TemplateVariablesJson",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "WhatsAppTemplateId",
                table: "Broadcasts");
        }
    }
}
