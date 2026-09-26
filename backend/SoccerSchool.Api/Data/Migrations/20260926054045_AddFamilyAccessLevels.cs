using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyAccessLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessLevel",
                table: "ParentContacts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteSentAt",
                table: "ParentContacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitedByUserId",
                table: "ParentContacts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessLevel",
                table: "ParentAccountCollaborators",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "ParentContacts");

            migrationBuilder.DropColumn(
                name: "InviteSentAt",
                table: "ParentContacts");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "ParentContacts");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "ParentAccountCollaborators");
        }
    }
}
