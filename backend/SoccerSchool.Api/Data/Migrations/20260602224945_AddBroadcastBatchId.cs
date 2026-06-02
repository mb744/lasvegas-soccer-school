using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastBatchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "Broadcasts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_BatchId_CreatedAt",
                table: "Broadcasts",
                columns: new[] { "BatchId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Broadcasts_BatchId_CreatedAt",
                table: "Broadcasts");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "Broadcasts");
        }
    }
}
