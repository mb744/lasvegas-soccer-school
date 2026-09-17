using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatModerationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Broadcasts_Players_PlayerId",
                table: "Broadcasts");

            migrationBuilder.CreateTable(
                name: "ChatMessageReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatMessageId = table.Column<int>(type: "int", nullable: false),
                    ReporterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReporterName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessageReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessageReports_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatUserBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BlockedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatUserBlocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageReports_ChatMessageId",
                table: "ChatMessageReports",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageReports_ResolvedAt_ReportedAt",
                table: "ChatMessageReports",
                columns: new[] { "ResolvedAt", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatUserBlocks_BlockerUserId",
                table: "ChatUserBlocks",
                column: "BlockerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatUserBlocks_BlockerUserId_BlockedUserId",
                table: "ChatUserBlocks",
                columns: new[] { "BlockerUserId", "BlockedUserId" },
                unique: true);

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

            migrationBuilder.DropTable(
                name: "ChatMessageReports");

            migrationBuilder.DropTable(
                name: "ChatUserBlocks");

            migrationBuilder.AddForeignKey(
                name: "FK_Broadcasts_Players_PlayerId",
                table: "Broadcasts",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id");
        }
    }
}
