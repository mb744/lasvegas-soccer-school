using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessagingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AutoReplyEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AutoReplyTextEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AutoReplyTextEs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagingSettings", x => x.Id);
                });

            // Seed the singleton row with the same canned text the previous (hardcoded) auto-reply
            // used so production behavior is identical until an admin edits it.
            migrationBuilder.Sql(
                "SET IDENTITY_INSERT [MessagingSettings] ON; " +
                "INSERT INTO [MessagingSettings] ([Id], [AutoReplyEnabled], [AutoReplyTextEn], [AutoReplyTextEs], [UpdatedAt]) " +
                "VALUES (1, 1, " +
                "N'Thanks for your message! An admin will reply soon. For urgent matters, please call the team. — Las Vegas Soccer School', " +
                "N'¡Gracias por escribirnos! Un administrador le responderá pronto. Si es urgente, llame al equipo. — Las Vegas Soccer School', " +
                "SYSUTCDATETIME()); " +
                "SET IDENTITY_INSERT [MessagingSettings] OFF;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessagingSettings");
        }
    }
}
