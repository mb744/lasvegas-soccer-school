namespace SoccerSchool.Api.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public string PublicBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Active season label, e.g. "2026/27". Stamped onto new Registrations.</summary>
    public string ActiveSeason { get; set; } = "2026/27";

    public CorsOptions Cors { get; set; } = new();
    public AdminBootstrapOptions Admin { get; set; } = new();
    public OAuthOptions OAuth { get; set; } = new();

    public class CorsOptions
    {
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }

    public class AdminBootstrapOptions
    {
        /// <summary>If set, an admin Identity user with this email is ensured at startup
        /// and granted the Admin role. Used to seed the first admin.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Initial password used only when creating the bootstrap admin
        /// (ignored if the user already exists).</summary>
        public string Password { get; set; } = string.Empty;
    }

    public class OAuthOptions
    {
        public ProviderOptions Google { get; set; } = new();
        public FacebookOptions Facebook { get; set; } = new();
    }

    public class ProviderOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    public class FacebookOptions
    {
        public string AppId { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret);
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
