using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers.Mobile;

/// <summary>The caller's kids plus the teams each is rostered on — drives the "filter schedule by
/// child" control in the app. Family-scoped via <see cref="IParentAccountResolver"/>.</summary>
[ApiController]
[Route("api/mobile/players")]
[Authorize(AuthenticationSchemes = AuthSchemes.MobileJwt)]
public class MobilePlayersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IParentAccountResolver _accounts;

    public MobilePlayersController(AppDbContext db, IParentAccountResolver accounts)
    {
        _db = db;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MobilePlayerDto>>> List(CancellationToken ct)
    {
        var account = await _accounts.ResolveAsync(User, ct);
        if (account is null) return Unauthorized();

        var players = await _db.Players
            .Where(p => p.ParentAccountId == account.Id)
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new MobilePlayerDto(
                p.Id, p.FirstName, p.LastName, p.DateOfBirth,
                _db.TeamPlayers
                    .Where(tp => tp.PlayerId == p.Id)
                    .Select(tp => new MobilePlayerTeamDto(tp.TeamId, tp.Team!.Name))
                    .ToList()))
            .ToListAsync(ct);

        return Ok(players);
    }
}
