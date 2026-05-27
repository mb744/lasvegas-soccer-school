using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundBroadcastLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BroadcastId",
                table: "InboundMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundMessages_BroadcastId",
                table: "InboundMessages",
                column: "BroadcastId");

            migrationBuilder.AddForeignKey(
                name: "FK_InboundMessages_Broadcasts_BroadcastId",
                table: "InboundMessages",
                column: "BroadcastId",
                principalTable: "Broadcasts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InboundMessages_Broadcasts_BroadcastId",
                table: "InboundMessages");

            migrationBuilder.DropIndex(
                name: "IX_InboundMessages_BroadcastId",
                table: "InboundMessages");

            migrationBuilder.DropColumn(
                name: "BroadcastId",
                table: "InboundMessages");
        }
    }
}
