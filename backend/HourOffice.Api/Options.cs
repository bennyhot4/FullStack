namespace HourOffice.Api;

public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "";
    public string Database { get; set; } = "houroffice";
}

public sealed class AuthOptions
{
    /// <summary>OAuth client id from Google Cloud Console. Required for Google sign-in.</summary>
    public string GoogleClientId { get; set; } = "";
    /// <summary>At least 32 characters. Must come from a secret store in production.</summary>
    public string JwtSigningKey { get; set; } = "";
    public string JwtIssuer { get; set; } = "houroffice";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
    /// <summary>Emails that become admins on first sign-in.</summary>
    public List<string> AdminEmails { get; set; } = new();
    /// <summary>When false, only users an admin added ("invited") may sign in.</summary>
    public bool AllowOpenSignup { get; set; } = true;
    /// <summary>Email-only sign-in for local development and tests. Ignored outside Development/Testing.</summary>
    public bool DevLoginEnabled { get; set; }
}

public sealed class CorsOptions
{
    public List<string> AllowedOrigins { get; set; } = new();
}

public sealed class BookingOptions
{
    public int OpenHour { get; set; } = 8;
    public int CloseHour { get; set; } = 22;
    public int MaxDaysAhead { get; set; } = 14;
    public string TimeZone { get; set; } = "Asia/Jerusalem";
    public int RecurringMaxDays { get; set; } = 366;
}

public sealed class CurrencyOptions
{
    /// <summary>ISO 4217 code stored on offices and bookings.</summary>
    public string Base { get; set; } = "ILS";
    /// <summary>Symbol shown per UI language. Same amount, different symbol (current behaviour).</summary>
    public Dictionary<string, string> DisplaySymbols { get; set; } = new() { ["he"] = "₪", ["en"] = "$" };
}

public sealed class StorageOptions
{
    /// <summary>"Local" (files on disk, served by the API) or "S3" (Cloudflare R2 / any S3-compatible store).</summary>
    public string Provider { get; set; } = "Local";
    public string LocalPath { get; set; } = "uploads";
    public long MaxBytes { get; set; } = 5 * 1024 * 1024;
    public S3StorageOptions S3 { get; set; } = new();
}

public sealed class S3StorageOptions
{
    public string ServiceUrl { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    /// <summary>Public base URL the bucket is served from, e.g. https://img.example.com</summary>
    public string PublicBaseUrl { get; set; } = "";
}

/// <summary>The MCP server and its OAuth authorization server (plan 011). All values come from config / env vars
/// (<c>Mcp__*</c>); the keys come only from the secret store.</summary>
public sealed class McpOptions
{
    /// <summary>Master switch (kill switch). When false, /mcp, /oauth/* and the OAuth metadata answer 503.</summary>
    public bool Enabled { get; set; }
    /// <summary>The API's public base URL, e.g. https://houroffice-api-xyz.run.app/ (the OAuth issuer).</summary>
    public string Issuer { get; set; } = "";
    /// <summary>The site's public base URL, where the consent page /McpConsent lives.</summary>
    public string SiteUrl { get; set; } = "";
    /// <summary>Browser origins allowed to call /mcp (DNS-rebinding defence). Requests with no Origin header are allowed.</summary>
    public List<string> AllowedOrigins { get; set; } = new();
    /// <summary>RSA private key (PEM) that signs the tokens. Required in Production.</summary>
    public string SigningKey { get; set; } = "";
    /// <summary>256-bit AES key (base64) that encrypts the access tokens. Required in Production.</summary>
    public string EncryptionKey { get; set; } = "";
    /// <summary>Assistants shown as "verified" when every redirect URI they register is in their list.</summary>
    public List<McpKnownClient> KnownClients { get; set; } = new();

    /// <summary>Per person, through MCP only (the site has no such limit).</summary>
    public int BookingWritesPerHour { get; set; } = 10;
    public int AdminWritesPerHour { get; set; } = 30;
    /// <summary>Recurring bookings via MCP cover at most this many days per call (the site allows Booking:RecurringMaxDays).</summary>
    public int RecurringMaxDays { get; set; } = 90;
    /// <summary>Per access token.</summary>
    public int CallsPerMinute { get; set; } = 60;
    /// <summary>Per IP, on /oauth/*.</summary>
    public int OAuthRequestsPerMinute { get; set; } = 30;
    public int RegistrationsPerHour { get; set; } = 10;
    /// <summary>admin_upload_image limit (the site keeps Storage:MaxBytes).</summary>
    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>The protected resource: tokens are issued for this audience only.</summary>
    public string Resource => Issuer.TrimEnd('/') + "/mcp";
}

public sealed class McpKnownClient
{
    public string Name { get; set; } = "";
    public List<string> RedirectUris { get; set; } = new();
}
