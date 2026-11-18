using MongoDB.Bson;
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
    public string? PhoneNumber { get; set; }
    public string? PictureUrl { get; set; }
    public string Role { get; set; } = Roles.User;
    /// <summary>"active" or "invited" (added by an admin, has not signed in yet).</summary>
    public string Status { get; set; } = "active";
    public int? TermsAcceptedVersion { get; set; }
    public DateTime? TermsAcceptedAt { get; set; }
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
    public string Notes { get; set; } = "";
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
    public List<string> Phones { get; set; } = new();
    /// <summary>0–2 lower-cased addresses.</summary>
    public List<string> Emails { get; set; } = new();
    public string Language { get; set; } = "he";
    /// <summary>The client agreed to get messages (future feature); covers all of the card's phones and emails.</summary>
    public bool WhatsAppConsent { get; set; }
    public DateTime? WhatsAppConsentAt { get; set; }
    public string Notes { get; set; } = "";
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
