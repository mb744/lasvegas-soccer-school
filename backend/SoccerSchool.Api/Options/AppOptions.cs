namespace SoccerSchool.Api.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public string PublicBaseUrl { get; set; } = "http://localhost:5173";
    public string AdminApiKey { get; set; } = "dev-admin-key-change-me";
    public CorsOptions Cors { get; set; } = new();

    public class CorsOptions
    {
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }
}

public class AcsOptions
{
    public const string SectionName = "Acs";

    public string ConnectionString { get; set; } = string.Empty;
    public string EmailFromAddress { get; set; } = string.Empty;
    public string SmsFromNumber { get; set; } = string.Empty;

    public bool IsEmailConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(EmailFromAddress);

    public bool IsSmsConfigured =>
        !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(SmsFromNumber);
}
