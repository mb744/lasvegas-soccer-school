using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    FromPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TwilioSid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundMessages_FromPhone",
                table: "InboundMessages",
                column: "FromPhone");

            migrationBuilder.CreateIndex(
                name: "IX_InboundMessages_ReceivedAt",
                table: "InboundMessages",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundMessages");
        }
    }
}
