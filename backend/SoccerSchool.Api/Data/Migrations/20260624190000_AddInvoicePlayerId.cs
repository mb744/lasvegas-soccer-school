using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePlayerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlayerId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PlayerId",
                table: "Invoices",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Players_PlayerId",
                table: "Invoices",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Players_PlayerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PlayerId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "Invoices");
        }
    }
}
