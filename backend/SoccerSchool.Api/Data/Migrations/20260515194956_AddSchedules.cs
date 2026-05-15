using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    GotSportEventId = table.Column<int>(type: "int", nullable: false),
                    GotSportTeamId = table.Column<int>(type: "int", nullable: false),
                    MessageGroupId = table.Column<int>(type: "int", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_MessageGroups_MessageGroupId",
                        column: x => x.MessageGroupId,
                        principalTable: "MessageGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledGames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    ExternalUid = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OpponentName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsHome = table.Column<bool>(type: "bit", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledGames_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledGames_StartsAt",
                table: "ScheduledGames",
                column: "StartsAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledGames_TeamId_ExternalUid",
                table: "ScheduledGames",
                columns: new[] { "TeamId", "ExternalUid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_MessageGroupId",
                table: "Teams",
                column: "MessageGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledGames");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
