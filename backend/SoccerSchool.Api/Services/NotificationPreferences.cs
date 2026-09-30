using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>Whose preferences a link in an event email changes: a login, or a family contact
/// without one. Exactly one is set.</summary>
public record PreferenceSubject(string? UserId, int? ContactId)
{
    public static PreferenceSubject ForUser(string userId) => new(userId, null);
    public static PreferenceSubject ForContact(int contactId) => new(null, contactId);
}

/// <summary>
/// Signed "Update your preferences" links in event emails. They don't expire (like an unsubscribe
/// link) and only open that one person's notification settings, nothing else.
/// </summary>
public interface IPreferenceTokens
{
    string Create(PreferenceSubject subject);
    PreferenceSubject? Read(string token);
}

public class PreferenceTokens : IPreferenceTokens
{
    private readonly IDataProtector _protector;

    public PreferenceTokens(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("LVSS.NotificationPrefs.v1");

    public string Create(PreferenceSubject subject) =>
        _protector.Protect(subject.UserId is not null ? $"u:{subject.UserId}" : $"c:{subject.ContactId}");

    public PreferenceSubject? Read(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var payload = _protector.Unprotect(token);
            if (payload.StartsWith("u:") && payload.Length > 2) return PreferenceSubject.ForUser(payload[2..]);
            if (payload.StartsWith("c:") && int.TryParse(payload[2..], out var id)) return PreferenceSubject.ForContact(id);
            return null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null; // tampered
        }
    }
}

public static class NotificationPreferenceRules
{
    /// <summary>Default (the club default) and Email both send; only an explicit "Don't email" stops it.</summary>
    public static bool WantsEmail(ScheduledEventKind kind, EmailPreference games, EmailPreference events) =>
        (kind == ScheduledEventKind.Game ? games : events) != EmailPreference.DontEmail;

    /// <summary>The user ids that haven't turned push notifications off.</summary>
    public static async Task<List<string>> WithoutMutedAsync(AppDbContext db, IReadOnlyCollection<string> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new List<string>();
        var muted = await db.Users.Where(u => userIds.Contains(u.Id) && u.PushMuted).Select(u => u.Id).ToListAsync(ct);
        return muted.Count == 0 ? userIds.ToList() : userIds.Except(muted).ToList();
    }
}
