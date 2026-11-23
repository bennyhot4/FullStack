using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using HourOffice.Api.Endpoints;

namespace HourOffice.Api.Mcp;

/// <summary>Text written by people (office descriptions, notes, names) goes back to the assistant only through
/// these helpers: trimmed to a maximum length, control and bidi-override characters removed (plan 011, S4).</summary>
public static class McpText
{
    public static string Clean(string? s, int max = 500)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var b = new StringBuilder(Math.Min(s.Length, max));
        foreach (var r in s.EnumerateRunes())
        {
            if (b.Length + r.Utf16SequenceLength > max) break;
            if (r.Value is 0x0A or 0x09) { b.Append(r.ToString()); continue; }
            if (IsHidden(r)) continue;
            b.Append(r.ToString());
        }
        return b.ToString().Trim();
    }

    /// <summary>Characters that hide or reorder text, used to smuggle instructions to the model: controls, formatting
    /// characters (bidi overrides, zero-width, tag characters U+E0000-E007F), variation selectors, line/paragraph
    /// separators, private use and lone surrogates (EnumerateRunes yields U+FFFD for those). ZWJ/ZWNJ stay: emoji and
    /// some scripts need them.</summary>
    private static bool IsHidden(Rune r)
    {
        if (r.Value is 0x200C or 0x200D) return false;
        if (r.Value is 0xFFFD) return true;
        if (r.Value is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF) return true;
        return Rune.GetUnicodeCategory(r) is UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate;
    }

    public static List<string> Clean(IEnumerable<string> list, int max = 100) =>
        list.Select(x => Clean(x, max)).Where(x => x.Length > 0).ToList();
}

public sealed record OfficeOut(string OfficeId, string Name, string Description, string Location, int Capacity,
    decimal HourlyRate, string Currency, List<string> Amenities)
{
    public static OfficeOut From(OfficeDto o) => new(o.Id, McpText.Clean(o.Name, 100), McpText.Clean(o.Description, 2000),
        McpText.Clean(o.Location, 200), o.Capacity, o.HourlyRate, o.Currency, McpText.Clean(o.Amenities));
}

public sealed record OfficesOut(List<OfficeOut> Offices);

public sealed record AdminOfficeOut(string OfficeId, string Name, string Description, string Location, int Capacity,
    decimal HourlyRate, string Currency, List<string> Amenities, string ImageUrl, bool IsAvailable)
{
    public static AdminOfficeOut From(OfficeDto o) => new(o.Id, McpText.Clean(o.Name, 100), McpText.Clean(o.Description, 2000),
        McpText.Clean(o.Location, 200), o.Capacity, o.HourlyRate, o.Currency, McpText.Clean(o.Amenities),
        McpText.Clean(o.ImageUrl, 2000), o.IsAvailable);
}

public sealed record AdminOfficesOut(List<AdminOfficeOut> Offices);

/// <summary>Free and taken hours of one day. Never who booked them.</summary>
public sealed record AvailabilityOut(string OfficeId, string Date, List<string> FreeSlots, List<string> BookedSlots,
    string Today, int MaxDaysAhead, string Note);

public sealed record BookingRulesOut(int OpenHour, int CloseHour, int MaxDaysAhead, string TimeZone, string Currency,
    int SlotMinutes, string Today);

/// <summary>A booking as its owner sees it. <see cref="ClientName"/> only with the clients:basic permission.</summary>
public sealed record BookingOut(string BookingId, string OfficeId, string OfficeName, string Date, string StartTime,
    string EndTime, int DurationHours, decimal TotalPrice, string Currency, string Status, string Notes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClientId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClientName)
{
    public static BookingOut From(MyBookingDto b, bool withClient) => new(b.Id, b.OfficeId, McpText.Clean(b.OfficeName, 100),
        b.Date, b.StartTime, b.EndTime, b.DurationHours, b.TotalPrice, b.Currency, b.Status, McpText.Clean(b.Notes, 1000),
        withClient ? b.ClientId : null, withClient && b.ClientName is { } n ? McpText.Clean(n, 130) : null);

    public static BookingOut From(BookingDto b) => new(b.Id, b.OfficeId, McpText.Clean(b.OfficeName, 100), b.Date,
        b.StartTime, b.EndTime, b.DurationHours, b.TotalPrice, b.Currency, b.Status, McpText.Clean(b.Notes, 1000), null, null);
}

public sealed record MyBookingsOut(List<BookingOut> Upcoming, List<BookingOut> Past);

/// <summary>A client. With clients:basic only id and name; phones, emails and notes exist only with clients:full
/// (the keys are left out of the JSON otherwise).</summary>
public sealed record ClientOut(string ClientId, string FirstName, string LastName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? Phones,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? Emails,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Notes)
{
    public static ClientOut Basic(Client c) => new(c.Id, McpText.Clean(c.FirstName, 60), McpText.Clean(c.LastName, 60),
        null, null, null);

    public static ClientOut Full(Client c) => new(c.Id, McpText.Clean(c.FirstName, 60), McpText.Clean(c.LastName, 60),
        c.Phones.ToList(), c.Emails.ToList(), McpText.Clean(c.Notes, Client.MaxNotes));
}

public sealed record ClientsOut(List<ClientOut> Clients);

public sealed record AdminBookingOut(string BookingId, string OfficeId, string OfficeName, string UserId, string BookerName,
    string Date, string StartTime, string EndTime, int DurationHours, decimal TotalPrice, string Currency, string Status,
    string Notes, string? SeriesId)
{
    /// <summary>Booking notes are personal data: only with <c>clients:full</c> (review M3).</summary>
    public static AdminBookingOut From(BookingDto b, bool full) => new(b.Id, b.OfficeId, McpText.Clean(b.OfficeName, 100), b.UserId,
        McpText.Clean(b.BookerName, 130), b.Date, b.StartTime, b.EndTime, b.DurationHours, b.TotalPrice, b.Currency,
        b.Status, full ? McpText.Clean(b.Notes, 1000) : "", b.SeriesId);
}

public sealed record AdminBookingsOut(List<AdminBookingOut> Bookings, int Count);

public sealed record RecurringOut(int Created, int Skipped, string SeriesId);

public sealed record UserOut(string UserId, string Email, string FullName, string? PhoneNumber, string Role, string Status,
    bool TermsAccepted, DateTime CreatedAt)
{
    /// <summary>Phone numbers only with <c>clients:full</c> (review M3); the email identifies the account.</summary>
    public static UserOut From(UserDto u, bool full) => new(u.Id, u.Email, McpText.Clean(u.FullName, 130), full ? u.PhoneNumber : null, u.Role,
        u.Status, u.TermsAccepted, u.CreatedAt);
}

public sealed record UsersOut(List<UserOut> Users);

public sealed record AnalyticsUserOut(string UserId, string FullName, string Email, int Bookings, int Hours, decimal Spent);

public sealed record AnalyticsOut(string Period, string From, string To, int TotalBookings, int TotalHours, decimal Revenue,
    List<AnalyticsUserOut> ByUser)
{
    public static AnalyticsOut Of(AnalyticsDto a) => new(a.Period, a.From, a.To, a.TotalBookings, a.TotalHours, a.Revenue,
        a.ByUser.Select(u => new AnalyticsUserOut(u.UserId, McpText.Clean(u.FullName, 130), u.Email, u.Bookings, u.Hours,
            u.Spent)).ToList());
}

public sealed record BrandingOut(string Title, string Description, string Logo, string ButtonsColor, string BackgroundColor,
    string PagesColor)
{
    public static BrandingOut From(IReadOnlyDictionary<string, string> b) => new(McpText.Clean(b[SettingKeys.BrandTitle], 200),
        McpText.Clean(b[SettingKeys.BrandDescription], 2000), McpText.Clean(b[SettingKeys.BrandLogo], 2000),
        b[SettingKeys.BrandButtonsColor], b[SettingKeys.BrandBackgroundColor], b[SettingKeys.BrandPagesColor]);
}

public sealed record UploadOut(string Purpose, string Url, string? BannerId);

public sealed record DoneOut(string Message);

public sealed record BannerOut(string BannerId, string Url);

public sealed record BrandingWithBannersOut(BrandingOut Branding, List<BannerOut> Banners);
