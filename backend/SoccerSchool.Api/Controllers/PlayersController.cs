using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;

namespace SoccerSchool.Api.Controllers;

[ApiController]
[Route("api/players")]
[Authorize]
public class PlayersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public PlayersController(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlayerSummary>>> List(CancellationToken ct)
    {
        var account = await GetAccountAsync(ct);
        if (account is null) return Unauthorized();

        var players = await _db.Players
            .Where(p => p.ParentAccountId == account.Id)
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new PlayerSummary(p.Id, p.FirstName, p.LastName, p.DateOfBirth))
            .ToListAsync(ct);
        return Ok(players);
    }

    [HttpPost]
    public async Task<ActionResult<PlayerSummary>> Create([FromBody] SavePlayerRequest req, CancellationToken ct)
    {
        var account = await GetAccountAsync(ct);
        if (account is null) return Unauthorized();

        var player = new Player
        {
            ParentAccountId = account.Id,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            DateOfBirth = req.DateOfBirth
        };
        _db.Players.Add(player);
        await _db.SaveChangesAsync(ct);
        return Ok(new PlayerSummary(player.Id, player.FirstName, player.LastName, player.DateOfBirth));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PlayerSummary>> Update(int id, [FromBody] SavePlayerRequest req, CancellationToken ct)
    {
        var account = await GetAccountAsync(ct);
        if (account is null) return Unauthorized();

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == id && p.ParentAccountId == account.Id, ct);
        if (player is null) return NotFound();

        player.FirstName = req.FirstName.Trim();
        player.LastName = req.LastName.Trim();
        player.DateOfBirth = req.DateOfBirth;
        await _db.SaveChangesAsync(ct);
        return Ok(new PlayerSummary(player.Id, player.FirstName, player.LastName, player.DateOfBirth));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var account = await GetAccountAsync(ct);
        if (account is null) return Unauthorized();

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == id && p.ParentAccountId == account.Id, ct);
        if (player is null) return NotFound();

        // Block delete if player has been used in a registration (preserves historical waivers).
        var inUse = await _db.RegistrationPlayers.AnyAsync(rp => rp.PlayerId == id, ct);
        if (inUse) return Conflict("Player has registration history and cannot be deleted.");

        _db.Players.Remove(player);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ParentAccount?> GetAccountAsync(CancellationToken ct)
    {
        var userId = _users.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return null;
        return await _db.ParentAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
    }
}
