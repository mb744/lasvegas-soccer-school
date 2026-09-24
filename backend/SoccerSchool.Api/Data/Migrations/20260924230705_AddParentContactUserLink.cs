using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParentContactUserLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ParentContacts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentContacts_UserId",
                table: "ParentContacts",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParentContacts_AspNetUsers_UserId",
                table: "ParentContacts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParentContacts_AspNetUsers_UserId",
                table: "ParentContacts");

            migrationBuilder.DropIndex(
                name: "IX_ParentContacts_UserId",
                table: "ParentContacts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ParentContacts");
        }
    }
}
