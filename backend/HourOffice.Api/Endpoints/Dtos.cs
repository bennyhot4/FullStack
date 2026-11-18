using HourOffice.Api.Services;

namespace HourOffice.Api.Endpoints;

public sealed record UserDto(
    string Id, string Email, string FullName, string FirstName, string LastName, string? PhoneNumber,
    string? PictureUrl, string Role, string Status, bool TermsAccepted, int? TermsAcceptedVersion,
    DateTime? TermsAcceptedAt, DateTime CreatedAt)
{
    public static UserDto From(User u, int currentTermsVersion) => new(
        u.Id, u.Email, u.FullName, u.FirstName, u.LastName, u.PhoneNumber, u.PictureUrl, u.Role, u.Status,
        u.TermsAcceptedVersion is { } v && v >= currentTermsVersion && currentTermsVersion > 0,
        u.TermsAcceptedVersion, u.TermsAcceptedAt, u.CreatedAt);
}

public sealed record OfficeDto(
    string Id, string Name, string Description, string ImageUrl, int Capacity, decimal HourlyRate,
    string Currency, List<string> Amenities, string Location, bool IsAvailable, DateTime CreatedAt)
{
    public static OfficeDto From(Office o) => new(o.Id, o.Name, o.Description, o.ImageUrl, o.Capacity, o.HourlyRate,
        o.Currency, o.Amenities, o.Location, o.IsAvailable, o.CreatedAt);
}

public sealed record BookingDto(
    string Id, string OfficeId, string OfficeName, string UserId, string BookerName, string Date,
    string StartTime, string EndTime, int DurationHours, decimal TotalPrice, string Currency, string Status,
    string Notes, string? SeriesId, DateTime? CancelledAt, DateTime CreatedAt)
{
    public static BookingDto From(Booking b, BookingRules rules) => new(b.Id, b.OfficeId, b.OfficeName, b.UserId,
        b.BookerName, b.Date, b.StartTime, b.EndTime, b.DurationHours, b.TotalPrice, b.Currency,
        rules.EffectiveStatus(b), b.Notes, b.SeriesId, b.CancelledAt, b.CreatedAt);
}

/// <summary>A booking as its owner sees it: the shared fields plus the linked client. Admin and shared
/// routes use <see cref="BookingDto"/>, which has no client fields.</summary>
public sealed record MyBookingDto(
    string Id, string OfficeId, string OfficeName, string UserId, string BookerName, string Date,
    string StartTime, string EndTime, int DurationHours, decimal TotalPrice, string Currency, string Status,
    string Notes, string? SeriesId, DateTime? CancelledAt, DateTime CreatedAt, string? ClientId, string? ClientName)
{
    public static MyBookingDto From(Booking b, BookingRules rules, string? clientName) => new(b.Id, b.OfficeId,
        b.OfficeName, b.UserId, b.BookerName, b.Date, b.StartTime, b.EndTime, b.DurationHours, b.TotalPrice,
        b.Currency, rules.EffectiveStatus(b), b.Notes, b.SeriesId, b.CancelledAt, b.CreatedAt, b.ClientId, clientName);
}

/// <summary>The client's id is returned as "clientId" (never "id").</summary>
public sealed record ClientDto(
    string ClientId, string FirstName, string LastName, List<string> Phones, List<string> Emails, string Language,
    bool WhatsAppConsent, DateTime? WhatsAppConsentAt, string Notes, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static ClientDto From(Client c) => new(c.Id, c.FirstName, c.LastName, c.Phones, c.Emails, c.Language,
        c.WhatsAppConsent, c.WhatsAppConsentAt, c.Notes, c.CreatedAt, c.UpdatedAt);

    public static string FullName(Client c) => $"{c.FirstName} {c.LastName}".Trim();
}

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, UserDto User);

// Requests
public sealed record GoogleLoginRequest(string? Credential);
public sealed record DevLoginRequest(string? Email, string? Name);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record UpdateProfileRequest(string? FirstName, string? LastName, string? Phone);
public sealed record AcceptTermsRequest(int Version);
public sealed record OfficeRequest(string? Name, string? Description, string? ImageUrl, int Capacity, decimal HourlyRate,
    List<string>? Amenities, string? Location, bool IsAvailable);
public sealed record CreateBookingRequest(string? OfficeId, string? Date, string? StartTime, string? EndTime, string? Notes,
    string? ClientId = null);
public sealed record SetBookingClientRequest(string? ClientId);
/// <summary>No UserId: the owner always comes from the signed-in user.</summary>
public sealed record ClientRequest(string? FirstName, string? LastName, List<string?>? Phones, List<string?>? Emails,
    string? Language, bool WhatsAppConsent, string? Notes);
public sealed record AdminUpdateBookingRequest(string? OfficeId, string? BookerName, string? Date, string? StartTime,
    string? EndTime, string? Notes);
public sealed record RecurringBookingRequest(string? OfficeId, string? UserId, string? BookerName, string? StartTime,
    string? EndTime, List<int>? Days, string? FromDate, string? ToDate, string? Notes);
public sealed record InviteUserRequest(string? Email, string? Role);
public sealed record AdminUpdateUserRequest(string? FullName, string? Email, string? PhoneNumber, string? Role);
public sealed record BrandingRequest(string? Title, string? Description, string? Logo, string? ButtonsColor,
    string? BackgroundColor, string? PagesColor);
public sealed record SettingValueRequest(string? Value);
public sealed record TermsRequest(string? ContentHe, string? ContentEn);
