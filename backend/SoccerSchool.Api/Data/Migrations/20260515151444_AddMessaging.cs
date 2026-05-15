using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Broadcasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TargetLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Broadcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    TwilioConversationSid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BroadcastRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BroadcastId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TwilioSid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StatusMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BroadcastRecipients_Broadcasts_BroadcastId",
                        column: x => x.BroadcastId,
                        principalTable: "Broadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupConversationParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupConversationId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TwilioParticipantSid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupConversationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupConversationParticipants_GroupConversations_GroupConversationId",
                        column: x => x.GroupConversationId,
                        principalTable: "GroupConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageGroupId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ParentAccountId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageGroupMembers_MessageGroups_MessageGroupId",
                        column: x => x.MessageGroupId,
                        principalTable: "MessageGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageGroupMembers_ParentAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "ParentAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_BroadcastId",
                table: "BroadcastRecipients",
                column: "BroadcastId");

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_TwilioSid",
                table: "BroadcastRecipients",
                column: "TwilioSid");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_CreatedAt",
                table: "Broadcasts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GroupConversationParticipants_GroupConversationId",
                table: "GroupConversationParticipants",
                column: "GroupConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupConversationParticipants_TwilioParticipantSid",
                table: "GroupConversationParticipants",
                column: "TwilioParticipantSid");

            migrationBuilder.CreateIndex(
                name: "IX_GroupConversations_TwilioConversationSid",
                table: "GroupConversations",
                column: "TwilioConversationSid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageGroupMembers_MessageGroupId_Phone",
                table: "MessageGroupMembers",
                columns: new[] { "MessageGroupId", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageGroupMembers_ParentAccountId",
                table: "MessageGroupMembers",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageGroups_Name",
                table: "MessageGroups",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BroadcastRecipients");

            migrationBuilder.DropTable(
                name: "GroupConversationParticipants");

            migrationBuilder.DropTable(
                name: "MessageGroupMembers");

            migrationBuilder.DropTable(
                name: "Broadcasts");

            migrationBuilder.DropTable(
                name: "GroupConversations");

            migrationBuilder.DropTable(
                name: "MessageGroups");
        }
    }
}
