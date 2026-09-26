using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileAppInstalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileAppInstalls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstallationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    AppVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    BuildNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PushPermission = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    HasPushToken = table.Column<bool>(type: "bit", nullable: false),
                    PushError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileAppInstalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobileAppInstalls_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppInstalls_InstallationId",
                table: "MobileAppInstalls",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileAppInstalls_UserId",
                table: "MobileAppInstalls",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileAppInstalls");
        }
    }
}
