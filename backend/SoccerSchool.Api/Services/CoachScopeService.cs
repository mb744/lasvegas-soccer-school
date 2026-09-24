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
/// Resolves coach access from the explicit <see cref="TeamCoach.UserId"/> link: a coach card linked
/// to your login is what makes you that team's coach. Links are made by an admin (Users page) or by
/// the sign-in reconcile that matches card emails — never by an email match here, so nothing reads
/// an unlinked card's email as proof of identity.
/// </summary>
public interface ICoachScopeService
{
    Task<StaffScope> GetScopeAsync(ClaimsPrincipal principal, CancellationToken ct);

    /// <summary>Team ids whose coach card is linked to this user. Empty for non-coaches.</summary>
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

    public async Task<IReadOnlyList<int>> GetCoachTeamIdsAsync(ApplicationUser user, CancellationToken ct) =>
        await _db.TeamCoaches
            .Where(tc => tc.UserId == user.Id)
            .Select(tc => tc.TeamId)
            .Distinct()
            .ToListAsync(ct);
}
