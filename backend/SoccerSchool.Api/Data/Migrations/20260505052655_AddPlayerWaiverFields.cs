using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerWaiverFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WaiverSignatureName",
                table: "Registrations");

            migrationBuilder.AddColumn<string>(
                name: "SignatureDataUrl",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAt",
                table: "Players",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverEmail",
                table: "Players",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverParentGuardianName",
                table: "Players",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverParticipantName",
                table: "Players",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverPhone",
                table: "Players",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverTeamName",
                table: "Players",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureDataUrl",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SignedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WaiverEmail",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WaiverParentGuardianName",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WaiverParticipantName",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WaiverPhone",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WaiverTeamName",
                table: "Players");

            migrationBuilder.AddColumn<string>(
                name: "WaiverSignatureName",
                table: "Registrations",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }
    }
}
