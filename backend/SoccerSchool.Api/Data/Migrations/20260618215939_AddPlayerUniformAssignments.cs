using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerUniformAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerUniformAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    UniformId = table.Column<int>(type: "int", nullable: false),
                    JerseyNumber = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AssignedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerUniformAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerUniformAssignments_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerUniformAssignments_Uniforms_UniformId",
                        column: x => x.UniformId,
                        principalTable: "Uniforms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUniformAssignments_PlayerId",
                table: "PlayerUniformAssignments",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUniformAssignments_PlayerId_AssignedAt",
                table: "PlayerUniformAssignments",
                columns: new[] { "PlayerId", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerUniformAssignments_UniformId",
                table: "PlayerUniformAssignments",
                column: "UniformId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerUniformAssignments");
        }
    }
}
