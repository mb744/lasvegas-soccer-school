using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReclaimEmailHashToParentAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReclaimEmailHash",
                table: "ParentAccounts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentAccounts_ReclaimEmailHash",
                table: "ParentAccounts",
                column: "ReclaimEmailHash",
                filter: "[ReclaimEmailHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParentAccounts_ReclaimEmailHash",
                table: "ParentAccounts");

            migrationBuilder.DropColumn(
                name: "ReclaimEmailHash",
                table: "ParentAccounts");
        }
    }
}
