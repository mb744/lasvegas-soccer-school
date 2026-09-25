using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediaAssetId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlobName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduledGameId = table.Column<int>(type: "int", nullable: false),
                    MediaAssetId = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UploaderName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReportCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventMedia_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventMedia_ScheduledGames_ScheduledGameId",
                        column: x => x.ScheduledGameId,
                        principalTable: "ScheduledGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_MediaAssetId",
                table: "ChatMessages",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_EventMedia_MediaAssetId",
                table: "EventMedia",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_EventMedia_ScheduledGameId_CreatedAt",
                table: "EventMedia",
                columns: new[] { "ScheduledGameId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_BlobName",
                table: "MediaAssets",
                column: "BlobName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Status_CreatedAt",
                table: "MediaAssets",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_MediaAssets_MediaAssetId",
                table: "ChatMessages",
                column: "MediaAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_MediaAssets_MediaAssetId",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "EventMedia");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_MediaAssetId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "MediaAssetId",
                table: "ChatMessages");
        }
    }
}
