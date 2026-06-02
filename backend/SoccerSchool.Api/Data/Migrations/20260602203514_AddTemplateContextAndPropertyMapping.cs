using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateContextAndPropertyMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PropertyKey",
                table: "WhatsAppTemplateVariables",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Context",
                table: "WhatsAppTemplates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill: tag every existing tournamentparticipation* template with the
            // TournamentConfirmation context (=1) and set the standard property mapping on
            // variables 1/2/3 (dates, player name, cost per player). Matches the previously
            // hard-coded send pipeline so the next send "just works" without admin edits.
            // Idempotent — uses WHERE clauses that only update unmapped rows.
            migrationBuilder.Sql(@"
                UPDATE WhatsAppTemplates
                SET Context = 1
                WHERE Context = 0
                  AND Name LIKE 'tournamentparticipation%';

                UPDATE v
                SET v.PropertyKey =
                    CASE v.Position
                        WHEN 1 THEN 'tournament.dates'
                        WHEN 2 THEN 'player.fullName'
                        WHEN 3 THEN 'tournament.costPerPlayer'
                    END
                FROM WhatsAppTemplateVariables v
                INNER JOIN WhatsAppTemplates t ON t.Id = v.WhatsAppTemplateId
                WHERE t.Context = 1
                  AND v.PropertyKey IS NULL
                  AND v.Position IN (1, 2, 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PropertyKey",
                table: "WhatsAppTemplateVariables");

            migrationBuilder.DropColumn(
                name: "Context",
                table: "WhatsAppTemplates");
        }
    }
}
