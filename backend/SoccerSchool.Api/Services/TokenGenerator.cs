using System.Security.Cryptography;

namespace SoccerSchool.Api.Services;

public static class TokenGenerator
{
    public static string New()
    {
        // 24 bytes -> 32 url-safe characters
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
