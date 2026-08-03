using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SoccerSchool.Api.Data;
using SoccerSchool.Api.Domain;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>The pair handed to a device on login/refresh: a short-lived signed JWT and an opaque
/// long-lived refresh token (whose hash we store).</summary>
public record TokenPair(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt);

/// <summary>
/// Issues and rotates the bearer/refresh tokens used by the mobile app. Access tokens are HMAC
/// JWTs carrying the user id + email + admin flag; refresh tokens are random opaque strings stored
/// only as SHA-256 hashes (<see cref="MobileRefreshToken"/>). The whole surface is no-op unless
/// <c>App:Jwt:SigningKey</c> is configured.
/// </summary>
public interface IMobileTokenService
{
    Task<TokenPair> IssueAsync(ApplicationUser user, CancellationToken ct);

    /// <summary>Validates a presented refresh token, revokes it, and issues a fresh pair (rotation).
    /// Returns null if the token is unknown, expired, or already revoked.</summary>
    Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken ct);

    /// <summary>Revokes the matching refresh token (logout). Silent if it doesn't exist.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}

public class MobileTokenService : IMobileTokenService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppOptions.JwtOptions _jwt;

    public MobileTokenService(AppDbContext db, UserManager<ApplicationUser> users, IOptions<AppOptions> app)
    {
        _db = db;
        _users = users;
        _jwt = app.Value.Jwt;
    }

    public async Task<TokenPair> IssueAsync(ApplicationUser user, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var accessExpires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var roles = await _users.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: creds);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Opaque refresh token — only its hash is persisted.
        var rawRefresh = GenerateRefreshToken();
        var refreshExpires = now.AddDays(_jwt.RefreshTokenDays);
        _db.MobileRefreshTokens.Add(new MobileRefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(rawRefresh),
            ExpiresAt = refreshExpires,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        return new TokenPair(accessToken, accessExpires, rawRefresh, refreshExpires);
    }

    public async Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        var hash = Hash(refreshToken);
        var row = await _db.MobileRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (row is null || !row.IsActive) return null;

        var user = await _users.FindByIdAsync(row.UserId);
        if (user is null || await _users.IsLockedOutAsync(user)) return null;

        // Rotate: kill the presented token, then mint a fresh pair.
        row.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await IssueAsync(user, ct);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = Hash(refreshToken);
        var row = await _db.MobileRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (row is null || row.RevokedAt is not null) return;
        row.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}
