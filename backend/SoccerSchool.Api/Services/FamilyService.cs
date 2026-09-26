using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>One person on a family, for the Profile tab's family list.</summary>
public record FamilyMember(
    /// <summary>"owner", "guardian" or "viewer".</summary>
    string Role,
    string Name,
    string? Email,
    /// <summary>"joined" (has signed in and is linked) or "invited" (not signed in yet).</summary>
    string Status,
    /// <summary>Set for invited/listed people; the id used to resend, change or remove.</summary>
    int? ContactId,
    /// <summary>Set for people linked without a contact row (an admin linked them).</summary>
    int? CollaboratorId,
    DateTime? InviteSentAt,
    bool IsYou);

public enum InviteOutcome { Sent, Linked, AlreadyMember, IsOwner, InvalidEmail, EmailFailed }

public interface IFamilyService
{
    /// <summary>Links every additional-parent entry (invite or registration contact) whose email
    /// matches this verified login, at the entry's access level. Called on every mobile sign-in /
    /// profile load. Returns true when anything changed.</summary>
    Task<bool> LinkByVerifiedEmailAsync(ApplicationUser user, CancellationToken ct);

    /// <summary>True when there's an unlinked invite or contact for this email.</summary>
    Task<bool> HasPendingInviteAsync(string email, CancellationToken ct);

    Task<List<FamilyMember>> ListMembersAsync(ParentAccount family, string callerUserId, CancellationToken ct);

    Task<(InviteOutcome Outcome, ParentContact? Contact)> InviteAsync(
        ParentAccount family, ApplicationUser inviter, string firstName, string lastName, string email,
        FamilyAccessLevel level, Language language, CancellationToken ct);

    Task<InviteOutcome> ResendAsync(ParentAccount family, ParentContact contact, ApplicationUser inviter, CancellationToken ct);

    Task SetAccessAsync(ParentAccount family, ParentContact contact, FamilyAccessLevel level, CancellationToken ct);

    Task RemoveContactAsync(ParentAccount family, ParentContact contact, CancellationToken ct);

    Task RemoveCollaboratorAsync(ParentAccountCollaborator link, CancellationToken ct);

    /// <summary>Drops team-chat memberships a login no longer has a claim to (after being removed
    /// from a family or made view-only). Keeps chats of teams their other families still play on.</summary>
    Task PruneChatMembershipsAsync(string userId, CancellationToken ct);
}

public class FamilyService : IFamilyService
{
    private const string AppStoreUrl = "https://apps.apple.com/us/app/id6812537499";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IParentAccountResolver _accounts;
    private readonly IEmailSender _email;
    private readonly AppOptions _app;
    private readonly ILogger<FamilyService> _logger;
    private readonly IWebHostEnvironment _env;

    public FamilyService(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IParentAccountResolver accounts,
        IEmailSender email,
        IOptions<AppOptions> app,
        ILogger<FamilyService> logger,
        IWebHostEnvironment env)
    {
        _env = env;
        _db = db;
        _users = users;
        _accounts = accounts;
        _email = email;
        _app = app.Value;
        _logger = logger;
    }

    public async Task<bool> LinkByVerifiedEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        // Email matching is only trusted once the login has proven it owns the address; entries
        // that are already linked by UserId apply either way.
        var normalizedEmail = user.EmailConfirmed ? user.NormalizedEmail : null;
        var contacts = await _db.ParentContacts
            .Where(c => c.UserId == user.Id
                || (c.UserId == null && normalizedEmail != null && c.Email != null
                    && c.Email.Trim().ToUpper() == normalizedEmail))
            .ToListAsync(ct);
        if (contacts.Count == 0) return false;

        var changed = false;
        foreach (var c in contacts.Where(c => c.UserId is null))
        {
            c.UserId = user.Id;
            changed = true;
        }

        // A contact on the user's own family would be a link to themselves; skip it.
        var ownedIds = await _db.ParentAccounts.Where(a => a.UserId == user.Id).Select(a => a.Id).ToListAsync(ct);
        var byFamily = contacts
            .Where(c => !ownedIds.Contains(c.ParentAccountId))
            .GroupBy(c => c.ParentAccountId)
            // Two entries for one family: the more generous level wins.
            .ToDictionary(g => g.Key, g => g.Min(c => c.AccessLevel));

        var links = await _db.ParentAccountCollaborators
            .Where(x => x.UserId == user.Id && byFamily.Keys.Contains(x.ParentAccountId))
            .ToListAsync(ct);
        foreach (var (familyId, level) in byFamily)
        {
            var link = links.FirstOrDefault(l => l.ParentAccountId == familyId);
            if (link is null)
            {
                _db.ParentAccountCollaborators.Add(new ParentAccountCollaborator
                {
                    ParentAccountId = familyId,
                    UserId = user.Id,
                    AccessLevel = level,
                });
                changed = true;
            }
            else if (link.AccessLevel != level)
            {
                link.AccessLevel = level;
                changed = true;
            }
        }

        if (changed) await _db.SaveChangesAsync(ct);
        return changed;
    }

    public async Task<bool> HasPendingInviteAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToUpperInvariant();
        return await _db.ParentContacts.AnyAsync(
            c => c.UserId == null && c.Email != null && c.Email.Trim().ToUpper() == normalized, ct);
    }

    public async Task<List<FamilyMember>> ListMembersAsync(ParentAccount family, string callerUserId, CancellationToken ct)
    {
        var members = new List<FamilyMember>();

        var ownerEmail = family.UserId is null
            ? null
            : await _db.Users.Where(u => u.Id == family.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        members.Add(new FamilyMember(
            "owner", $"{family.FirstName} {family.LastName}".Trim(), ownerEmail, "joined",
            null, null, null, family.UserId == callerUserId));

        var contacts = await _db.ParentContacts
            .Where(c => c.ParentAccountId == family.Id)
            .OrderBy(c => c.AccessLevel).ThenBy(c => c.FirstName).ThenBy(c => c.LastName)
            .ToListAsync(ct);
        var links = await _db.ParentAccountCollaborators
            .Where(c => c.ParentAccountId == family.Id)
            .Select(c => new
            {
                c.Id, c.UserId, c.AccessLevel,
                Email = c.User != null ? c.User.Email : null,
                OwnFirst = _db.ParentAccounts.Where(p => p.UserId == c.UserId).Select(p => p.FirstName).FirstOrDefault(),
                OwnLast = _db.ParentAccounts.Where(p => p.UserId == c.UserId).Select(p => p.LastName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        foreach (var c in contacts)
        {
            var joined = c.UserId is not null && links.Any(l => l.UserId == c.UserId);
            members.Add(new FamilyMember(
                RoleName(c.AccessLevel), $"{c.FirstName} {c.LastName}".Trim(), c.Email,
                joined ? "joined" : "invited", c.Id, null, c.InviteSentAt, c.UserId == callerUserId));
        }

        // Logins linked without a contact row (an admin linked them from the website).
        var contactUserIds = contacts.Where(c => c.UserId != null).Select(c => c.UserId!).ToHashSet();
        foreach (var l in links.Where(l => !contactUserIds.Contains(l.UserId)))
        {
            var name = string.IsNullOrWhiteSpace(l.OwnFirst) ? (l.Email ?? "") : $"{l.OwnFirst} {l.OwnLast}".Trim();
            members.Add(new FamilyMember(
                RoleName(l.AccessLevel), name, l.Email, "joined", null, l.Id, null, l.UserId == callerUserId));
        }
        return members;
    }

    public async Task<(InviteOutcome Outcome, ParentContact? Contact)> InviteAsync(
        ParentAccount family, ApplicationUser inviter, string firstName, string lastName, string email,
        FamilyAccessLevel level, Language language, CancellationToken ct)
    {
        email = email.Trim();
        if (!new EmailAddressAttribute().IsValid(email) || email.Length > 256)
            return (InviteOutcome.InvalidEmail, null);
        var normalized = email.ToUpperInvariant();

        if (family.UserId is not null
            && await _db.Users.AnyAsync(u => u.Id == family.UserId && u.NormalizedEmail == normalized, ct))
            return (InviteOutcome.IsOwner, null);

        var existingUser = await _users.FindByEmailAsync(email);
        if (existingUser is not null && await _db.ParentAccountCollaborators.AnyAsync(
                c => c.ParentAccountId == family.Id && c.UserId == existingUser.Id, ct))
            return (InviteOutcome.AlreadyMember, null);

        // Re-inviting someone already listed (e.g. from the registration form) updates that entry
        // instead of adding a duplicate.
        var contact = await _db.ParentContacts.FirstOrDefaultAsync(
            c => c.ParentAccountId == family.Id && c.Email != null && c.Email.Trim().ToUpper() == normalized, ct);
        if (contact is null)
        {
            contact = new ParentContact { ParentAccountId = family.Id };
            _db.ParentContacts.Add(contact);
        }
        contact.FirstName = firstName.Trim();
        contact.LastName = lastName.Trim();
        contact.Email = email;
        contact.AccessLevel = level;
        contact.Language = language;
        contact.InvitedByUserId = inviter.Id;
        await _db.SaveChangesAsync(ct);

        // Someone who already has a verified login is linked right away; they'll see the family
        // the next time the app refreshes. Everyone else is linked when they first sign in.
        if (existingUser is not null && existingUser.EmailConfirmed)
        {
            await LinkByVerifiedEmailAsync(existingUser, ct);
            if (level == FamilyAccessLevel.Viewer) await PruneChatMembershipsAsync(existingUser.Id, ct);
        }

        var sent = await SendInviteAsync(family, inviter, contact, ct);
        if (!sent) return (InviteOutcome.EmailFailed, contact);
        return (existingUser is { EmailConfirmed: true } ? InviteOutcome.Linked : InviteOutcome.Sent, contact);
    }

    public async Task<InviteOutcome> ResendAsync(ParentAccount family, ParentContact contact, ApplicationUser inviter, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contact.Email)) return InviteOutcome.InvalidEmail;
        return await SendInviteAsync(family, inviter, contact, ct) ? InviteOutcome.Sent : InviteOutcome.EmailFailed;
    }

    public async Task SetAccessAsync(ParentAccount family, ParentContact contact, FamilyAccessLevel level, CancellationToken ct)
    {
        contact.AccessLevel = level;
        if (contact.UserId is not null)
        {
            var link = await _db.ParentAccountCollaborators.FirstOrDefaultAsync(
                c => c.ParentAccountId == family.Id && c.UserId == contact.UserId, ct);
            if (link is not null) link.AccessLevel = level;
        }
        await _db.SaveChangesAsync(ct);
        if (contact.UserId is not null && level == FamilyAccessLevel.Viewer)
            await PruneChatMembershipsAsync(contact.UserId, ct);
    }

    public async Task RemoveContactAsync(ParentAccount family, ParentContact contact, CancellationToken ct)
    {
        var userId = contact.UserId;
        _db.ParentContacts.Remove(contact);
        if (userId is not null)
        {
            var links = await _db.ParentAccountCollaborators
                .Where(c => c.ParentAccountId == family.Id && c.UserId == userId)
                .ToListAsync(ct);
            _db.ParentAccountCollaborators.RemoveRange(links);
        }
        await _db.SaveChangesAsync(ct);
        if (userId is not null) await PruneChatMembershipsAsync(userId, ct);
    }

    public async Task RemoveCollaboratorAsync(ParentAccountCollaborator link, CancellationToken ct)
    {
        var userId = link.UserId;
        _db.ParentAccountCollaborators.Remove(link);
        await _db.SaveChangesAsync(ct);
        await PruneChatMembershipsAsync(userId, ct);
    }

    public async Task PruneChatMembershipsAsync(string userId, CancellationToken ct)
    {
        var guardianFamilies = await _accounts.FamilyIdsAsync(userId, guardianOnly: true, ct);
        var teamIds = guardianFamilies.Count == 0
            ? new List<int>()
            : await _db.TeamPlayers
                .Where(tp => guardianFamilies.Contains(tp.Player!.ParentAccountId))
                .Select(tp => tp.TeamId)
                .Distinct()
                .ToListAsync(ct);

        // Only parent rows seeded for a team's chat; admin/coach rows and non-team groups stay.
        var stale = await _db.ChatGroupMembers
            .Where(m => m.UserId == userId && m.Role == ChatMemberRole.Parent
                && m.ChatGroup!.TeamId != null && !teamIds.Contains(m.ChatGroup.TeamId!.Value))
            .ToListAsync(ct);
        if (stale.Count == 0) return;
        _db.ChatGroupMembers.RemoveRange(stale);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> SendInviteAsync(ParentAccount family, ApplicationUser inviter, ParentContact contact, CancellationToken ct)
    {
        var kids = await _db.Players
            .Where(p => p.ParentAccountId == family.Id)
            .OrderBy(p => p.FirstName)
            .Select(p => p.FirstName)
            .ToListAsync(ct);
        var inviterName = await _db.ParentAccounts
            .Where(p => p.UserId == inviter.Id)
            .Select(p => (p.FirstName + " " + p.LastName).Trim())
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(inviterName)) inviterName = inviter.Email ?? "A parent";

        var (subject, body) = contact.Language == Language.Spanish
            ? SpanishInvite(contact, inviterName, kids)
            : EnglishInvite(contact, inviterName, kids);

        if (!_email.IsAvailable && _env.IsDevelopment())
        {
            _logger.LogInformation("DEV ONLY — family invite for {Email}: {Subject} | {Body}", contact.Email, subject, body);
            contact.InviteSentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        var result = await _email.SendAsync(contact.Email!, subject, body, ct);
        if (!result.Success)
        {
            _logger.LogWarning("Family invite email to {Email} failed: {Message}", contact.Email, result.Message);
            return false;
        }
        contact.InviteSentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private (string Subject, string Body) EnglishInvite(ParentContact c, string inviter, List<string> kids)
    {
        var who = kids.Count == 0 ? "their family" : JoinNames(kids, "and");
        var access = c.AccessLevel == FamilyAccessLevel.Guardian
            ? "You'll be added as a parent/guardian: you'll see the schedule, confirm who's coming, get team messages and join the team chat."
            : "You'll be able to follow along: the schedule, game and practice details, photos and announcements.";
        var body =
            $"Hi {c.FirstName},\n\n" +
            $"{inviter} invited you to follow {who} at Las Vegas Soccer School in the LVSS app.\n\n" +
            $"{access}\n\n" +
            "How to join:\n" +
            $"1. Download the app on iPhone or iPad: {AppStoreUrl}\n" +
            "   (The Android app is coming soon.)\n" +
            $"2. Sign in with this email address: {c.Email}\n" +
            "   Use \"Continue with Google\" or \"Continue with Facebook\" if this is your Google or Facebook email.\n" +
            $"   Otherwise, create an account with this email at {BaseUrl()}/signup and confirm it from the email we send you, then sign in to the app.\n\n" +
            $"You'll be connected to {inviter}'s family automatically.\n\n" +
            "Las Vegas Soccer School";
        return ($"{inviter} invited you to the LVSS app", body);
    }

    private (string Subject, string Body) SpanishInvite(ParentContact c, string inviter, List<string> kids)
    {
        var who = kids.Count == 0 ? "su familia" : JoinNames(kids, "y");
        var access = c.AccessLevel == FamilyAccessLevel.Guardian
            ? "Será agregado como padre/tutor: verá el calendario, confirmará quién asiste, recibirá los mensajes del equipo y podrá participar en el chat del equipo."
            : "Podrá seguir de cerca: el calendario, los detalles de partidos y prácticas, fotos y anuncios.";
        var body =
            $"Hola {c.FirstName}:\n\n" +
            $"{inviter} lo invitó a seguir a {who} en Las Vegas Soccer School con la app de LVSS.\n\n" +
            $"{access}\n\n" +
            "Cómo unirse:\n" +
            $"1. Descargue la app en iPhone o iPad: {AppStoreUrl}\n" +
            "   (La app para Android estará disponible pronto.)\n" +
            $"2. Inicie sesión con este correo: {c.Email}\n" +
            "   Use \"Continuar con Google\" o \"Continuar con Facebook\" si es su correo de Google o Facebook.\n" +
            $"   Si no, cree una cuenta con este correo en {BaseUrl()}/signup, confírmela desde el correo que le enviaremos y luego inicie sesión en la app.\n\n" +
            $"Quedará conectado a la familia de {inviter} automáticamente.\n\n" +
            "Las Vegas Soccer School";
        return ($"{inviter} lo invitó a la app de LVSS", body);
    }

    private string BaseUrl() => (_app.PublicBaseUrl ?? string.Empty).TrimEnd('/');

    private static string JoinNames(List<string> names, string and) =>
        names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} {and} {names[^1]}";

    private static string RoleName(FamilyAccessLevel level) =>
        level == FamilyAccessLevel.Viewer ? "viewer" : "guardian";
}
