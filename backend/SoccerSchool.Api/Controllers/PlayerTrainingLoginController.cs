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
/// Parent-side management of a child's Daily Training login: see it, create it, rename it, set a
/// new password, or remove it. Family-scoped via <see cref="IParentAccountResolver"/> (owners and
/// collaborators). Accepts the web cookie and the parent app's mobile JWT.
/// </summary>
[ApiController]
[Route("api/players/{playerId:int}/training-login")]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrMobileJwt)]
public class PlayerTrainingLoginController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IParentAccountResolver _accounts;
    private readonly IPasswordHasher<PlayerLogin> _hasher;
    private readonly IPlayerTokenService _tokens;

    public PlayerTrainingLoginController(
        AppDbContext db,
        IParentAccountResolver accounts,
        IPasswordHasher<PlayerLogin> hasher,
        IPlayerTokenService tokens)
    {
        _db = db;
        _accounts = accounts;
        _hasher = hasher;
        _tokens = tokens;
    }

    [HttpGet]
    public async Task<ActionResult<PlayerTrainingLoginDto>> Get(int playerId, CancellationToken ct)
    {
        if (!await OwnsPlayerAsync(playerId, ct)) return NotFound();
        var login = await _db.PlayerLogins.AsNoTracking().FirstOrDefaultAsync(l => l.PlayerId == playerId, ct);
        return Ok(ToDto(login));
    }

    /// <summary>Creates the login, or updates the username and/or password. A password is required
    /// to create; on update, omitting it keeps the current one. Setting a password signs the child
    /// out of every device and clears any lockout.</summary>
    [HttpPut]
    public async Task<ActionResult<PlayerTrainingLoginDto>> Save(int playerId, [FromBody] SavePlayerTrainingLoginRequest req, CancellationToken ct)
    {
        if (!await OwnsPlayerAsync(playerId, ct)) return NotFound();

        var username = PlayerCredentials.NormalizeUsername(req.Username);
        var usernameError = PlayerCredentials.ValidateUsername(username);
        if (usernameError is not null) return BadRequest(usernameError);

        var login = await _db.PlayerLogins.FirstOrDefaultAsync(l => l.PlayerId == playerId, ct);
        var passwordProvided = !string.IsNullOrEmpty(req.Password);
        if (login is null && !passwordProvided) return BadRequest("A password is required.");
        if (passwordProvided)
        {
            var passwordError = PlayerCredentials.ValidatePassword(req.Password);
            if (passwordError is not null) return BadRequest(passwordError);
        }

        var taken = await _db.PlayerLogins.AnyAsync(l => l.Username == username && l.PlayerId != playerId, ct);
        if (taken) return Conflict("That username is already taken. Try adding a number.");

        var now = DateTime.UtcNow;
        if (login is null)
        {
            login = new PlayerLogin { PlayerId = playerId, CreatedAt = now };
            _db.PlayerLogins.Add(login);
        }
        login.Username = username;
        login.UpdatedAt = now;

        var passwordChanged = false;
        if (passwordProvided)
        {
            login.PasswordHash = _hasher.HashPassword(login, req.Password!);
            login.PasswordChangedAt = now;
            login.AccessFailedCount = 0;
            login.LockoutEnd = null;
            passwordChanged = true;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the unique username index.
            return Conflict("That username is already taken. Try adding a number.");
        }

        if (passwordChanged) await _tokens.RevokeAllAsync(login.Id, ct);
        return Ok(ToDto(login));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int playerId, CancellationToken ct)
    {
        if (!await OwnsPlayerAsync(playerId, ct)) return NotFound();
        var login = await _db.PlayerLogins.FirstOrDefaultAsync(l => l.PlayerId == playerId, ct);
        if (login is null) return NoContent();
        // Cascades to the child's refresh tokens and reset links; training history is kept on the player.
        _db.PlayerLogins.Remove(login);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> OwnsPlayerAsync(int playerId, CancellationToken ct)
    {
        var account = await _accounts.ResolveAsync(User, ct);
        return account is not null
            && await _db.Players.AnyAsync(p => p.Id == playerId && p.ParentAccountId == account.Id, ct);
    }

    private static PlayerTrainingLoginDto ToDto(PlayerLogin? login) =>
        login is null
            ? new PlayerTrainingLoginDto(false, null, null)
            : new PlayerTrainingLoginDto(true, login.Username, login.LastLoginAt);
}
