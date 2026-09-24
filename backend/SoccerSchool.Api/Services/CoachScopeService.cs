using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;

namespace SoccerSchool.Api.Services;

/// <summary>What a signed-in staff member may manage: everything (admin), or just the teams whose
/// coach card carries their email (coach).</summary>
public record StaffScope(bool IsAdmin, IReadOnlyList<int> CoachTeamIds)
{
    public bool IsCoach => CoachTeamIds.Count > 0;
    public bool IsStaff => IsAdmin || IsCoach;
    public bool CanManageTeam(int teamId) => IsAdmin || CoachTeamIds.Contains(teamId);
}

/// <summary>
/// Resolves coach access by matching the login's email against <see cref="TeamCoach.Email"/> —
/// the same rule <c>ChatAdminController</c> uses to add coaches to team chats. There is no separate
/// coach role: a coach card with your email on a team is what makes you that team's coach.
/// </summary>
public interface ICoachScopeService
{
    Task<StaffScope> GetScopeAsync(ClaimsPrincipal principal, CancellationToken ct);

    /// <summary>Team ids whose coach card matches this user's email. Empty for non-coaches.</summary>
    Task<IReadOnlyList<int>> GetCoachTeamIdsAsync(ApplicationUser user, CancellationToken ct);
}

public class CoachScopeService : ICoachScopeService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public CoachScopeService(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<StaffScope> GetScopeAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(principal);
        if (user is null) return new StaffScope(false, Array.Empty<int>());
        var isAdmin = await _users.IsInRoleAsync(user, Roles.Admin);
        return new StaffScope(isAdmin, await GetCoachTeamIdsAsync(user, ct));
    }

    public async Task<IReadOnlyList<int>> GetCoachTeamIdsAsync(ApplicationUser user, CancellationToken ct)
    {
        var normalized = user.NormalizedEmail;
        if (string.IsNullOrEmpty(normalized)) return Array.Empty<int>();
        return await _db.TeamCoaches
            .Where(tc => tc.Email != null && tc.Email.Trim().ToUpper() == normalized)
            .Select(tc => tc.TeamId)
            .Distinct()
            .ToListAsync(ct);
    }
}
