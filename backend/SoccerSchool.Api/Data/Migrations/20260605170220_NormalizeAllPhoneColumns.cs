using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <summary>
    /// Backfill: strip common punctuation from every phone-bearing column and re-prefix the
    /// result to E.164 (<c>+1XXXXXXXXXX</c> for US 10-digit, <c>+1XXXXXXXXXX</c> when an 11-digit
    /// 1-prefixed form is present). Without this, threads in the Inbox silently drop outbound
    /// rows whose <c>BroadcastRecipients.Phone</c> contains parens/dashes/spaces because
    /// <see cref="SoccerSchool.Api.Services.PhoneNormalizer.Variants(string)"/> doesn't generate
    /// punctuated forms — so the per-phone lookup never matches them.
    ///
    /// Covers every column that <see cref="SoccerSchool.Api.Services.PhoneNormalizer"/> is used
    /// for at write time, so old rows that pre-dated the write-time normalization also benefit.
    /// Rows whose stripped digits aren't a recognizable US 10/11-digit form are left alone —
    /// the original might be intentional (international, or a typo we don't want to mangle).
    /// </summary>
    public partial class NormalizeAllPhoneColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One reusable template per phone column. Strips +, parens, dashes, dots, spaces
            // (and tabs / non-breaking spaces, which occasionally show up from copy-paste) then
            // re-prefixes based on the resulting digit length.
            string Sql(string table, string col) => $@"
WITH cleaned AS (
    SELECT
        Id,
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([{col}], CHAR(160), ''), CHAR(9), ''), ' ', ''), '-', ''), '.', ''), '(', ''), ')', ''), '+', '') AS dig
    FROM [{table}]
    WHERE [{col}] IS NOT NULL AND [{col}] <> ''
)
UPDATE t
SET [{col}] = CASE
    WHEN c.dig NOT LIKE '%[^0-9]%' AND LEN(c.dig) = 10 THEN '+1' + c.dig
    WHEN c.dig NOT LIKE '%[^0-9]%' AND LEN(c.dig) = 11 AND c.dig LIKE '1%' THEN '+' + c.dig
    ELSE t.[{col}]
END
FROM [{table}] t
INNER JOIN cleaned c ON t.Id = c.Id
WHERE c.dig NOT LIKE '%[^0-9]%'
  AND (LEN(c.dig) = 10 OR (LEN(c.dig) = 11 AND c.dig LIKE '1%'))
  AND t.[{col}] <> CASE
        WHEN LEN(c.dig) = 10 THEN '+1' + c.dig
        ELSE '+' + c.dig
      END;
";

            migrationBuilder.Sql(Sql("ParentAccounts",   "CellPhone"));
            migrationBuilder.Sql(Sql("ParentContacts",   "CellPhone"));
            migrationBuilder.Sql(Sql("MessageGroupMembers", "Phone"));
            migrationBuilder.Sql(Sql("TeamCoaches",      "Phone"));
            migrationBuilder.Sql(Sql("Teams",            "CoachPhone"));
            migrationBuilder.Sql(Sql("BroadcastRecipients", "Phone"));
            migrationBuilder.Sql(Sql("InboundMessages",  "FromPhone"));
            migrationBuilder.Sql(Sql("InboundMessages",  "ToPhone"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible — we'd need to know which rows were touched and what they were
            // before, which we don't capture. No-op down.
        }
    }
}
