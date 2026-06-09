using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniforms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UniformId",
                table: "ScheduledGames",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Uniforms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ShirtColor = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ShortsColor = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SockColor = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Designation = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uniforms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledGames_UniformId",
                table: "ScheduledGames",
                column: "UniformId");

            migrationBuilder.CreateIndex(
                name: "IX_Uniforms_Designation",
                table: "Uniforms",
                column: "Designation",
                unique: true,
                filter: "[Designation] <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_Uniforms_Name",
                table: "Uniforms",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledGames_Uniforms_UniformId",
                table: "ScheduledGames",
                column: "UniformId",
                principalTable: "Uniforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledGames_Uniforms_UniformId",
                table: "ScheduledGames");

            migrationBuilder.DropTable(
                name: "Uniforms");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledGames_UniformId",
                table: "ScheduledGames");

            migrationBuilder.DropColumn(
                name: "UniformId",
                table: "ScheduledGames");
        }
    }
}
