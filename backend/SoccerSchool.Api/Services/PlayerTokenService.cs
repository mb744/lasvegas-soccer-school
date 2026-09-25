using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>
/// Issues and rotates Daily Training (kid) tokens. Mirrors <see cref="MobileTokenService"/> but
/// signs for the <c>PlayerAudience</c> and carries only player claims — no Identity user id, no
/// email, no roles — so nothing downstream can mistake a kid for a parent or admin.
/// </summary>
public interface IPlayerTokenService
{
    Task<TokenPair> IssueAsync(PlayerLogin login, CancellationToken ct);

    /// <summary>Validates, revokes and replaces a refresh token. Null if unknown/expired/revoked or
    /// the login is locked out.</summary>
    Task<(TokenPair Pair, PlayerLogin Login)?> RefreshAsync(string refreshToken, CancellationToken ct);

    Task RevokeAsync(string refreshToken, CancellationToken ct);

    /// <summary>Kills every session for a login — used when the parent changes the password.</summary>
    Task RevokeAllAsync(int playerLoginId, CancellationToken ct);
}

public class PlayerTokenService : IPlayerTokenService
{
    /// <summary>Claim carrying <see cref="PlayerLogin.Id"/>.</summary>
    public const string PlayerLoginIdClaim = "plid";

    /// <summary>Claim carrying <see cref="PlayerLogin.PasswordChangedAt"/> (unix milliseconds) at
    /// issue time. The training API rejects tokens whose stamp is older than the login's current
    /// one. Milliseconds, not seconds: a reset landing in the same second the login was created
    /// (or last changed) must still invalidate earlier tokens.</summary>
    public const string PasswordVersionClaim = "pwv";

    public static long PasswordVersion(PlayerLogin login) =>
        new DateTimeOffset(DateTime.SpecifyKind(login.PasswordChangedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private readonly AppDbContext _db;
    private readonly AppOptions.JwtOptions _jwt;

    public PlayerTokenService(AppDbContext db, IOptions<AppOptions> app)
    {
        _db = db;
        _jwt = app.Value.Jwt;
    }

    public async Task<TokenPair> IssueAsync(PlayerLogin login, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var accessExpires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, $"player-login:{login.Id}"),
            new(PlayerLoginIdClaim, login.Id.ToString()),
            new(PasswordVersionClaim, PasswordVersion(login).ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.PlayerAudience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var rawRefresh = PlayerCredentials.GenerateToken(48);
        var refreshExpires = now.AddDays(_jwt.RefreshTokenDays);
        _db.PlayerRefreshTokens.Add(new PlayerRefreshToken
        {
            PlayerLoginId = login.Id,
            TokenHash = PlayerCredentials.HashToken(rawRefresh),
            ExpiresAt = refreshExpires,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        return new TokenPair(accessToken, accessExpires, rawRefresh, refreshExpires);
    }

    public async Task<(TokenPair Pair, PlayerLogin Login)?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        var hash = PlayerCredentials.HashToken(refreshToken);
        var row = await _db.PlayerRefreshTokens
            .Include(t => t.PlayerLogin)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (row is null || !row.IsActive || row.PlayerLogin is null) return null;
        if (row.PlayerLogin.LockoutEnd > DateTime.UtcNow) return null;

        row.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await IssueAsync(row.PlayerLogin, ct), row.PlayerLogin);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = PlayerCredentials.HashToken(refreshToken);
        var row = await _db.PlayerRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (row is null || row.RevokedAt is not null) return;
        row.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(int playerLoginId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await _db.PlayerRefreshTokens
            .Where(t => t.PlayerLoginId == playerLoginId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active) t.RevokedAt = now;
        await _db.SaveChangesAsync(ct);
    }
}
