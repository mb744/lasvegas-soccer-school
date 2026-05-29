using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <summary>
    /// Seeds the Spanish for the two cancellation phrases the event cancel-&-notify flow stuffs into
    /// template variables: "Has been cancelled" (the game template's opponent slot) and "CANCELLED"
    /// (the practice template's date slot, prefixed onto the when value). Without these the bilingual
    /// preview would show the English phrase on the Spanish side.
    ///
    /// Idempotent: each row is inserted only when no entry with the same English value exists, so
    /// admins who already added these by hand won't hit a conflict.
    /// </summary>
    public partial class SeedCancellationPhrases : Migration
    {
        private static readonly (string En, string Es)[] Seed = new[]
        {
            ("Has been cancelled", "Ha sido cancelado"),
            ("CANCELLED", "CANCELADO"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (en, es) in Seed)
            {
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
