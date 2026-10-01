using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Players",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Players",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedReason",
                table: "Players",
                type: "int",
                nullable: true);

            // Kids of families that already deleted their account (ReclaimEmailHash is stamped by
            // the purge) are archived, reason FamilyDeleted (1), so they leave rosters and pickers.
            migrationBuilder.Sql(@"
UPDATE p SET p.ArchivedAt = SYSUTCDATETIME(), p.ArchivedReason = 1
FROM Players p
JOIN ParentAccounts a ON a.Id = p.ParentAccountId
WHERE a.ReclaimEmailHash IS NOT NULL AND p.ArchivedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ArchivedReason",
                table: "Players");
        }
    }
}
