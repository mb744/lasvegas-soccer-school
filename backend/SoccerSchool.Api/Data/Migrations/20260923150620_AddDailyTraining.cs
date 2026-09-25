using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<int>(type: "int", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TitleEs = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DescriptionEs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StepsEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    StepsEs = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Reps = table.Column<int>(type: "int", nullable: true),
                    VideoUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerLogins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerLogins_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrillAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrillId = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    AgeClassificationId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrillAssignments", x => x.Id);
                    table.CheckConstraint("CK_DrillAssignments_Target", "([TargetType] = 0 AND [PlayerId] IS NOT NULL AND [TeamId] IS NULL AND [AgeClassificationId] IS NULL) OR ([TargetType] = 1 AND [TeamId] IS NOT NULL AND [PlayerId] IS NULL AND [AgeClassificationId] IS NULL) OR ([TargetType] = 2 AND [AgeClassificationId] IS NOT NULL AND [PlayerId] IS NULL AND [TeamId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_DrillAssignments_AgeClassifications_AgeClassificationId",
                        column: x => x.AgeClassificationId,
                        principalTable: "AgeClassifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrillAssignments_Drills_DrillId",
                        column: x => x.DrillId,
                        principalTable: "Drills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrillAssignments_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrillAssignments_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrillCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    DrillId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrillCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrillCompletions_Drills_DrillId",
                        column: x => x.DrillId,
                        principalTable: "Drills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DrillCompletions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerPasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerLoginId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerPasswordResetTokens_PlayerLogins_PlayerLoginId",
                        column: x => x.PlayerLoginId,
                        principalTable: "PlayerLogins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerLoginId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerRefreshTokens_PlayerLogins_PlayerLoginId",
                        column: x => x.PlayerLoginId,
                        principalTable: "PlayerLogins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrillAssignments_AgeClassificationId",
                table: "DrillAssignments",
                column: "AgeClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_DrillAssignments_DrillId",
                table: "DrillAssignments",
                column: "DrillId");

            migrationBuilder.CreateIndex(
                name: "IX_DrillAssignments_PlayerId",
                table: "DrillAssignments",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DrillAssignments_StartDate_EndDate",
                table: "DrillAssignments",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DrillAssignments_TeamId",
                table: "DrillAssignments",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DrillCompletions_DrillId",
                table: "DrillCompletions",
                column: "DrillId");

            migrationBuilder.CreateIndex(
                name: "IX_DrillCompletions_PlayerId_Date_DrillId",
                table: "DrillCompletions",
                columns: new[] { "PlayerId", "Date", "DrillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drills_IsActive",
                table: "Drills",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLogins_PlayerId",
                table: "PlayerLogins",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLogins_Username",
                table: "PlayerLogins",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPasswordResetTokens_PlayerLoginId",
                table: "PlayerPasswordResetTokens",
                column: "PlayerLoginId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPasswordResetTokens_TokenHash",
                table: "PlayerPasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRefreshTokens_PlayerLoginId",
                table: "PlayerRefreshTokens",
                column: "PlayerLoginId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRefreshTokens_TokenHash",
                table: "PlayerRefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrillAssignments");

            migrationBuilder.DropTable(
                name: "DrillCompletions");

            migrationBuilder.DropTable(
                name: "PlayerPasswordResetTokens");

            migrationBuilder.DropTable(
                name: "PlayerRefreshTokens");

            migrationBuilder.DropTable(
                name: "Drills");

            migrationBuilder.DropTable(
                name: "PlayerLogins");
        }
    }
}
