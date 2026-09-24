using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Dtos;
using SoccerSchool.Api.Services;

namespace SoccerSchool.Api.Controllers;

/// <summary>
/// Backs the public <c>/reset-player-password?token=…</c> page a parent reaches from the email sent
/// by <c>PlayerAuthController.ForgotPassword</c>. The single-use token in the link is the
/// authorization — possession of it proves access to the parent's inbox.
/// </summary>
[ApiController]
[Route("api/public/player-password-reset")]
[AllowAnonymous]
public class PlayerPasswordResetController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<PlayerLogin> _hasher;
    private readonly IPlayerTokenService _tokens;

    public PlayerPasswordResetController(AppDbContext db, IPasswordHasher<PlayerLogin> hasher, IPlayerTokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    /// <summary>Who the link is for, so the page can say "Choose a new password for Sofia".
    /// 404 when the link is unknown, used, or expired.</summary>
    [HttpGet]
    public async Task<ActionResult<PlayerPasswordResetInfoDto>> Get([FromQuery] string token, CancellationToken ct)
    {
        var row = await FindUsableAsync(token, ct);
        if (row is null) return NotFound();
        return Ok(new PlayerPasswordResetInfoDto(row.PlayerLogin!.Player!.FirstName, row.PlayerLogin.Username));
    }

    [HttpPost]
    public async Task<IActionResult> Reset([FromBody] PlayerPasswordResetRequest req, CancellationToken ct)
    {
        var row = await FindUsableAsync(req.Token, ct);
        if (row is null) return NotFound();

        var error = PlayerCredentials.ValidatePassword(req.NewPassword);
        if (error is not null) return BadRequest(error);

        var now = DateTime.UtcNow;
        var login = row.PlayerLogin!;
        login.PasswordHash = _hasher.HashPassword(login, req.NewPassword);
        login.PasswordChangedAt = now;
        login.UpdatedAt = now;
        login.AccessFailedCount = 0;
        login.LockoutEnd = null;

        // Burn every outstanding link for this login, not just the one used.
        var outstanding = await _db.PlayerPasswordResetTokens
            .Where(t => t.PlayerLoginId == login.Id && t.UsedAt == null)
            .ToListAsync(ct);
        foreach (var t in outstanding) t.UsedAt = now;

        await _db.SaveChangesAsync(ct);
        await _tokens.RevokeAllAsync(login.Id, ct);
        return NoContent();
    }

    private async Task<PlayerPasswordResetToken?> FindUsableAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = PlayerCredentials.HashToken(token.Trim());
        var row = await _db.PlayerPasswordResetTokens
            .Include(t => t.PlayerLogin!).ThenInclude(l => l.Player)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        return row is { IsUsable: true, PlayerLogin.Player: not null } ? row : null;
    }
}
