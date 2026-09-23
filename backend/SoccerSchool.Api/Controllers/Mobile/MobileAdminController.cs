using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>
/// Read-only support endpoints for the mobile admin surface — reference lists the composer screens
/// need but that would be overkill to fetch through the full web-admin controllers (which have
/// heavier DTOs and cookie-only auth). Every action requires the Admin role authenticated via the
/// mobile JWT bearer scheme.
/// </summary>
[ApiController]
[Route("api/mobile/admin")]
[Authorize(Roles = Roles.Admin, AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobileAdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public MobileAdminController(AppDbContext db) { _db = db; }

    /// <summary>Team picker for the announcement composer — id + name only, alpha-sorted.</summary>
    [HttpGet("teams")]
    public async Task<ActionResult<IEnumerable<MobileTeamOptionDto>>> Teams(CancellationToken ct)
    {
        var rows = await _db.Teams
            .OrderBy(t => t.Name)
            .Select(t => new MobileTeamOptionDto(t.Id, t.Name))
            .ToListAsync(ct);
        return Ok(rows);
    }
}

public record MobileTeamOptionDto(int Id, string Name);
