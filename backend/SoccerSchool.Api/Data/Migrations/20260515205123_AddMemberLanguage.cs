using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Language",
                table: "MessageGroupMembers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill existing members from their group's language, so previously-saved groups
            // don't all snap to English. New members default to the group's language too via the
            // AddMember endpoint.
            migrationBuilder.Sql(@"
                UPDATE m
                SET m.[Language] = g.[Language]
                FROM [MessageGroupMembers] m
                INNER JOIN [MessageGroups] g ON g.[Id] = m.[MessageGroupId]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "MessageGroupMembers");
        }
    }
}
