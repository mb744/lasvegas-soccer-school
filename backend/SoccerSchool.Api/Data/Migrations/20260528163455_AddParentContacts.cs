using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParentContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentAccountId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CellPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    HasWhatsApp = table.Column<bool>(type: "bit", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentContacts_ParentAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "ParentAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentContacts_ParentAccountId",
                table: "ParentContacts",
                column: "ParentAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentContacts");
        }
    }
}
