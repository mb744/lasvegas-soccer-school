using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChargeTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChargeTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Recurrence = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargeTypes_Active",
                table: "ChargeTypes",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_ChargeTypes_Name",
                table: "ChargeTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargeTypeId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ChargeTypeId",
                table: "Invoices",
                column: "ChargeTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_ChargeTypes_ChargeTypeId",
                table: "Invoices",
                column: "ChargeTypeId",
                principalTable: "ChargeTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_ChargeTypes_ChargeTypeId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ChargeTypeId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ChargeTypeId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "ChargeTypes");
        }
    }
}
