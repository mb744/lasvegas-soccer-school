using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SoccerSchool.Api.Options;

namespace SoccerSchool.Api.Services;

/// <summary>
/// One-way hash used to remember a deleted mobile account's email address for later re-linking,
/// without storing the address itself. See <see cref="Domain.ParentAccount.ReclaimEmailHash"/>.
/// The salt is <see cref="AppOptions.JwtOptions.SigningKey"/> — already a required secret — so the
/// hash can only be recomputed by the running backend, not by anyone with a copy of the database.
/// Rotating the JWT signing key invalidates existing reclaim links, which is acceptable given
/// signing-key rotation is a rare, deliberate event.
/// </summary>
public interface IReclaimHasher
{
    /// <summary>Returns the reclaim hash for <paramref name="email"/>, or null if the address is
    /// empty. Case- and whitespace-insensitive.</summary>
    string? Hash(string? email);
}

public class ReclaimHasher : IReclaimHasher
{
    private readonly AppOptions.JwtOptions _jwt;

    public ReclaimHasher(IOptions<AppOptions> app)
    {
        _jwt = app.Value.Jwt;
    }

    public string? Hash(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !_jwt.IsConfigured) return null;
        var normalized = email.Trim().ToLowerInvariant();
        var input = Encoding.UTF8.GetBytes(_jwt.SigningKey + ":reclaim:" + normalized);
        return Convert.ToBase64String(SHA256.HashData(input));
    }
}
