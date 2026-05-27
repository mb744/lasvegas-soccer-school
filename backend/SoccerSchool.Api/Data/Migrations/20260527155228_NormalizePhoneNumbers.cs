using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoccerSchool.Api.Data.Migrations
{
    /// <summary>
    /// One-shot backfill that brings existing phone columns into E.164 (<c>+1XXXXXXXXXX</c>) form
    /// so Inbox lookups and per-phone matching work without variant-fallback logic. Applies to
    /// <c>ParentAccount.CellPhone</c> and <c>MessageGroupMember.Phone</c> — the two columns parents
    /// or admins type into directly. Broadcast/Inbound rows come from Twilio already in E.164.
    ///
    /// Handles the two common malformed cases:
    ///   1. Bare 10 digits, no punctuation → prepend <c>+1</c>.
    ///   2. 11 digits starting with <c>1</c>, no plus → prepend <c>+</c>.
    /// Rows with punctuation (parens, dashes, spaces) or already-E.164 values are left as-is —
    /// dashes etc. are rare in practice (the registration form is type="tel" but doesn't enforce
    /// a mask) and can be cleaned up via the admin Edit form on the few rows it affects.
    /// </summary>
    public partial class NormalizePhoneNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- ParentAccount.CellPhone ---
            // Strip punctuation to a digits-only working value, then re-prefix.
            // Pattern check uses NOT LIKE '%[^0-9]%' to detect "pure digits, nothing else".
            migrationBuilder.Sql(@"
                UPDATE [ParentAccounts]
                SET [CellPhone] = '+1' + [CellPhone]
                WHERE [CellPhone] IS NOT NULL
                  AND LEN([CellPhone]) = 10
                  AND [CellPhone] NOT LIKE '%[^0-9]%';

                UPDATE [ParentAccounts]
                SET [CellPhone] = '+' + [CellPhone]
                WHERE [CellPhone] IS NOT NULL
                  AND LEN([CellPhone]) = 11
                  AND [CellPhone] LIKE '1%'
                  AND [CellPhone] NOT LIKE '%[^0-9]%';
            ");

            // --- MessageGroupMember.Phone ---
            migrationBuilder.Sql(@"
                UPDATE [MessageGroupMembers]
                SET [Phone] = '+1' + [Phone]
                WHERE [Phone] IS NOT NULL
                  AND LEN([Phone]) = 10
                  AND [Phone] NOT LIKE '%[^0-9]%';

                UPDATE [MessageGroupMembers]
                SET [Phone] = '+' + [Phone]
                WHERE [Phone] IS NOT NULL
                  AND LEN([Phone]) = 11
                  AND [Phone] LIKE '1%'
                  AND [Phone] NOT LIKE '%[^0-9]%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Backfill is not reversible — we'd need to know which rows were touched, and the
            // original form might have been ambiguous. Down is a no-op; reverting this migration
            // just means newly-normalized phones stay in E.164 form, which is harmless.
        }
    }
}
