using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamCoaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamCoaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Language = table.Column<int>(type: "int", nullable: false),
                    HasWhatsApp = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamCoaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamCoaches_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamCoaches_TeamId",
                table: "TeamCoaches",
                column: "TeamId");

            // Backfill: every team that has any inline coach contact gets one TeamCoach row.
            // Name defaults to the inline CoachName when set, else a placeholder so admins know
            // to rename. Language/HasWhatsApp default to the entity defaults (English/false).
            migrationBuilder.Sql(@"
                INSERT INTO TeamCoaches (TeamId, Name, Email, Phone, Language, HasWhatsApp, CreatedAt)
                SELECT
                    t.Id,
                    COALESCE(NULLIF(LTRIM(RTRIM(t.CoachName)), ''), 'Coach') AS Name,
                    NULLIF(LTRIM(RTRIM(t.CoachEmail)), '') AS Email,
                    NULLIF(LTRIM(RTRIM(t.CoachPhone)), '') AS Phone,
                    0 AS Language,
                    0 AS HasWhatsApp,
                    SYSUTCDATETIME() AS CreatedAt
                FROM Teams t
                WHERE
                    (NULLIF(LTRIM(RTRIM(t.CoachName)),  '') IS NOT NULL)
                 OR (NULLIF(LTRIM(RTRIM(t.CoachEmail)), '') IS NOT NULL)
                 OR (NULLIF(LTRIM(RTRIM(t.CoachPhone)), '') IS NOT NULL);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamCoaches");
        }
    }
}
