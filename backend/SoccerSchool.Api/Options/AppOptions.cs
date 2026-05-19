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

public class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string SmsFromNumber { get; set; } = string.Empty;

    /// <summary>WhatsApp-enabled sender in E.164, e.g. "+14155551234". The "whatsapp:" channel
    /// prefix is added by the sender code, so store the bare number here.</summary>
    public string WhatsAppFromNumber { get; set; } = string.Empty;

    /// <summary>Content SID (HX...) of the approved WhatsApp template used for business-initiated
    /// messages. WhatsApp requires a pre-approved template outside the 24h customer-service window.</summary>
    public string WhatsAppTemplateSid { get; set; } = string.Empty;

    /// <summary>Conversations Service SID (IS...) for true group chats via the Twilio
    /// Conversations API. Optional; only needed for the group-chat feature.</summary>
    public string ConversationsServiceSid { get; set; } = string.Empty;

    /// <summary>True when AccountSid + AuthToken + SmsFromNumber are all set; OutreachSender prefers Twilio over ACS for SMS in that case.</summary>
    public bool IsSmsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) &&
        !string.IsNullOrWhiteSpace(AuthToken) &&
        !string.IsNullOrWhiteSpace(SmsFromNumber);

    /// <summary>True when WhatsApp business messaging can be used (credentials + a WhatsApp sender).</summary>
    public bool IsWhatsAppConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) &&
        !string.IsNullOrWhiteSpace(AuthToken) &&
        !string.IsNullOrWhiteSpace(WhatsAppFromNumber);

    /// <summary>True when the Conversations API (true group chat) can be used.</summary>
    public bool IsConversationsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) &&
        !string.IsNullOrWhiteSpace(AuthToken) &&
        !string.IsNullOrWhiteSpace(ConversationsServiceSid);
}
