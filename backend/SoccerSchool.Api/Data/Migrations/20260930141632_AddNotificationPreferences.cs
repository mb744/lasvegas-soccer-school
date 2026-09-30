using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventEmails",
                table: "ParentContacts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GameEmails",
                table: "ParentContacts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderEmailSentAt",
                table: "EventAttendances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventEmails",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GameEmails",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PushMuted",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventEmails",
                table: "ParentContacts");

            migrationBuilder.DropColumn(
                name: "GameEmails",
                table: "ParentContacts");

            migrationBuilder.DropColumn(
                name: "ReminderEmailSentAt",
                table: "EventAttendances");

            migrationBuilder.DropColumn(
                name: "EventEmails",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "GameEmails",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PushMuted",
                table: "AspNetUsers");
        }
    }
}
