using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <summary>
    /// Pre-populates the phrase dictionary with the abbreviations the messaging Compose tab
    /// produces when it autofills "When" from a picked game: short weekday names (Mon, Tue, ...),
    /// short month names (Jan, Feb, ...), and AM/PM. Without these the bilingual translation step
    /// can't render "Wed, 5/27 5:00 PM" as "Mié, 5/27 5:00 PM".
    ///
    /// Inserts are idempotent: each row is added only if no entry with the same English value
    /// already exists, so admins who've already added some entries by hand won't see conflicts.
    /// </summary>
    public partial class SeedWeekdayPhrases : Migration
    {
        private static readonly (string En, string Es)[] Seed = new[]
        {
            // Short weekdays — what toLocaleDateString(..., { weekday: 'short' }) emits in en-US.
            ("Mon", "Lun"),
            ("Tue", "Mar"),
            ("Wed", "Mié"),
            ("Thu", "Jue"),
            ("Fri", "Vie"),
            ("Sat", "Sáb"),
            ("Sun", "Dom"),
            // Long weekdays in case admin prefers full names in the template.
            ("Monday", "Lunes"),
            ("Tuesday", "Martes"),
            ("Wednesday", "Miércoles"),
            ("Thursday", "Jueves"),
            ("Friday", "Viernes"),
            ("Saturday", "Sábado"),
            ("Sunday", "Domingo"),
            // Short months.
            ("Jan", "Ene"),
            ("Feb", "Feb"),
            ("Mar", "Mar"),
            ("Apr", "Abr"),
            ("May", "May"),
            ("Jun", "Jun"),
            ("Jul", "Jul"),
            ("Aug", "Ago"),
            ("Sep", "Sep"),
            ("Oct", "Oct"),
            ("Nov", "Nov"),
            ("Dec", "Dic"),
            // AM/PM.
            ("AM", "AM"),
            ("PM", "PM"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (en, es) in Seed)
            {
                // Single-quote escape for SQL literals.
                var enLit = en.Replace("'", "''");
                var esLit = es.Replace("'", "''");
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [PhraseTranslations] WHERE [English] = N'{enLit}') " +
                    $"INSERT INTO [PhraseTranslations] ([English], [Spanish], [CreatedAt], [UpdatedAt]) " +
                    $"VALUES (N'{enLit}', N'{esLit}', SYSUTCDATETIME(), SYSUTCDATETIME());");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove only rows whose English value matches and whose Spanish value also matches
            // the seeded form — leaves any admin-overridden translations alone.
            foreach (var (en, es) in Seed)
            {
                var enLit = en.Replace("'", "''");
                var esLit = es.Replace("'", "''");
                migrationBuilder.Sql(
                    $"DELETE FROM [PhraseTranslations] WHERE [English] = N'{enLit}' AND [Spanish] = N'{esLit}';");
            }
        }
    }
}
