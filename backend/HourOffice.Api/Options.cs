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
