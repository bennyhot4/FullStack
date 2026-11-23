using MongoDB.Bson;
using HourOffice.Api.Services;
using MongoDB.Bson.Serialization.Attributes;

namespace HourOffice.Api;

public static class Roles
{
    public const string User = "user";
    public const string Admin = "admin";
    public static bool IsValid(string? r) => r is User or Admin;
}

public static class BookingStatus
{
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
    /// <summary>Not stored: a confirmed booking whose end time has passed is reported as completed.</summary>
    public const string Completed = "completed";
}

public sealed class User
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string Email { get; set; } = "";
    public string? GoogleSub { get; set; }
    public string FullName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    /// <summary>Encrypted at rest when FieldEncryption:Mode is encrypt (plan 011, E3).</summary>
    [BsonSerializer(typeof(EncryptedSerializer<string>))] public string? PhoneNumber { get; set; }
    public string? PictureUrl { get; set; }
    public string Role { get; set; } = Roles.User;
    /// <summary>"active" or "invited" (added by an admin, has not signed in yet).</summary>
    public string Status { get; set; } = "active";
    public int? TermsAcceptedVersion { get; set; }
    public DateTime? TermsAcceptedAt { get; set; }
    /// <summary>The user agreed to get updates and offers. Copied to the admins' clients made from this user.</summary>
    public bool MarketingConsent { get; set; }
    public DateTime? MarketingConsentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public sealed class Office
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public int Capacity { get; set; }
    public decimal HourlyRate { get; set; }
    public string Currency { get; set; } = "ILS";
    public List<string> Amenities { get; set; } = new();
    public string Location { get; set; } = "";
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class Booking
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonRepresentation(BsonType.ObjectId)] public string OfficeId { get; set; } = "";
    public string OfficeName { get; set; } = "";
    [BsonRepresentation(BsonType.ObjectId)] public string UserId { get; set; } = "";
    public string BookerName { get; set; } = "";
    /// <summary>yyyy-MM-dd in the business time zone.</summary>
    public string Date { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    /// <summary>One entry per booked hour ("09:00", "10:00"...). A unique index on (officeId, date, slots)
    /// for confirmed bookings makes double booking impossible at the database level.</summary>
    public List<string> Slots { get; set; } = new();
    public int DurationHours { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "ILS";
    public string Status { get; set; } = BookingStatus.Confirmed;
    [BsonSerializer(typeof(EncryptedSerializer<string>))] public string Notes { get; set; } = "";
    [BsonRepresentation(BsonType.ObjectId)] public string? SeriesId { get; set; }
    /// <summary>Optional link to one of the booking owner's clients. Only the owner ever sees it (never admins).</summary>
    [BsonRepresentation(BsonType.ObjectId)] public string? ClientId { get; set; }
    public DateTime? CancelledAt { get; set; }
    [BsonRepresentation(BsonType.ObjectId)] public string? CancelledById { get; set; }
    [BsonRepresentation(BsonType.ObjectId)] public string CreatedById { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>A person a user serves, kept in that user's private client list. Visible to its owner only.</summary>
public sealed class Client
{
    public const int MaxPerUser = 1000;
    public const int MaxPhones = 2;
    public const int MaxEmails = 2;
    public const int MaxNotes = 500;
    public static bool IsLanguage(string? l) => l is "he" or "en";

    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonRepresentation(BsonType.ObjectId)] public string UserId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    /// <summary>1–2 numbers in E.164 format.</summary>
    [BsonSerializer(typeof(EncryptedSerializer<List<string>>))] public List<string> Phones { get; set; } = new();
    /// <summary>0–2 lower-cased addresses.</summary>
    [BsonSerializer(typeof(EncryptedSerializer<List<string>>))] public List<string> Emails { get; set; } = new();
    public string Language { get; set; } = "he";
    /// <summary>The client agreed to get messages (future feature); covers all of the card's phones and emails.</summary>
    public bool WhatsAppConsent { get; set; }
    public DateTime? WhatsAppConsentAt { get; set; }
    [BsonSerializer(typeof(EncryptedSerializer<string>))] public string Notes { get; set; } = "";
    /// <summary>Set when the system added this client from a registered user (an admin's copy of that user).
    /// Not stored when null, so the unique (userId, sourceUserId) index ignores hand-typed clients.</summary>
    [BsonRepresentation(BsonType.ObjectId), BsonIgnoreIfNull] public string? SourceUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A named group of the owner's own clients, for future announcements. Private to its owner.</summary>
public sealed class DistributionList
{
    public const int MaxPerUser = 100;
    public const int MaxClients = 1000;
    public const int MaxName = 60;
    public const int MaxDescription = 300;

    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonRepresentation(BsonType.ObjectId)] public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    [BsonRepresentation(BsonType.ObjectId)] public List<string> ClientIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SiteSetting
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class TermsDoc
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public int Version { get; set; }
    public string ContentHe { get; set; } = "";
    public string ContentEn { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    [BsonRepresentation(BsonType.ObjectId)] public string? PublishedById { get; set; }
}

public sealed class RefreshToken
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    [BsonRepresentation(BsonType.ObjectId)] public string UserId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>When this session signed in with Google; copied unchanged to each rotated token (plan 011, S1).</summary>
    public DateTime? AuthTime { get; set; }
}

public static class SettingKeys
{
    public const string HomeBannerImage = "home_banner_image";
    public const string HomeBannerPrefix = "home_banner_";
    public const string BrandTitle = "brand_title";
    public const string BrandDescription = "brand_description";
    public const string BrandLogo = "brand_logo";
    public const string BrandButtonsColor = "brand_buttons_color";
    public const string BrandBackgroundColor = "brand_background_color";
    public const string BrandPagesColor = "brand_pages_color";
    /// <summary>Internal marker: the one-time fill of the admins' client lists has run (see AdminClientSync).</summary>
    public const string AdminClientsFilled = "admin_clients_filled";
    /// <summary>Admin switch for AI assistants (MCP): "false" turns it off. Missing = on (the env var is the master switch).</summary>
    public const string McpEnabled = "mcp_enabled";

    public static readonly Dictionary<string, string> BrandDefaults = new()
    {
        [BrandTitle] = "",
        [BrandDescription] = "",
        [BrandLogo] = "",
        [BrandButtonsColor] = "#3B6A8F",
        [BrandBackgroundColor] = "#F3F6F9",
        [BrandPagesColor] = "#FFFFFF",
    };
}

/// <summary>A pending OAuth authorization request waiting for the user's decision on the consent page (plan 011).
/// The id is a random secret; documents expire after a few minutes (TTL on <see cref="ExpiresAt"/>).</summary>
public sealed class McpConsentRequest
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Denied = "denied";
    public const string Used = "used";

    [BsonId] public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string? State { get; set; }
    /// <summary>The PKCE challenge of the first leg; the second leg must carry the same one.</summary>
    public string CodeChallenge { get; set; } = "";
    public List<string> RequestedScopes { get; set; } = new();
    /// <summary>The original authorization request parameters, replayed after approval.</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string Status { get; set; } = Pending;
    [BsonRepresentation(BsonType.ObjectId)] public string? UserId { get; set; }
    public List<string> GrantedScopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>One MCP tool call. Never stores arguments, tokens or personal data (plan 011, S8).</summary>
public sealed class McpAuditEntry
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public DateTime At { get; set; }
    [BsonRepresentation(BsonType.ObjectId)] public string? UserId { get; set; }
    public string Role { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Tool { get; set; } = "";
    /// <summary>"ok", "error" or "declined" (the user said no on the confirmation prompt).</summary>
    public string Outcome { get; set; } = "";
    public string? ErrorCode { get; set; }
    /// <summary>Id of the booking, office or setting the call changed.</summary>
    public string? TargetId { get; set; }
    public bool IsWrite { get; set; }
    public string IpHash { get; set; } = "";
    public long DurationMs { get; set; }
    /// <summary>TTL: 90 days, 365 days for admin writes.</summary>
    public DateTime ExpiresAt { get; set; }
}
