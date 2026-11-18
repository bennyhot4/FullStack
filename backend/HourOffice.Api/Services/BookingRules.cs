using System.Globalization;
using Microsoft.Extensions.Options;

namespace HourOffice.Api.Services;

public sealed record SlotRange(DateOnly Date, int StartHour, int EndHour)
{
    public string DateText => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string StartTime => BookingRules.HourText(StartHour);
    public string EndTime => BookingRules.HourText(EndHour);
    public int Hours => EndHour - StartHour;
    public List<string> Slots => Enumerable.Range(StartHour, Hours).Select(BookingRules.HourText).ToList();
}

/// <summary>All booking validation lives here so the server, not the browser, enforces the rules.</summary>
public sealed class BookingRules(IOptions<BookingOptions> options, TimeProvider clock)
{
    private readonly BookingOptions _o = options.Value;
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public BookingOptions Options => _o;

    public static string HourText(int h) => $"{h:00}:00";

    public DateTime LocalNow => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), _tz).DateTime;
    public DateOnly Today => DateOnly.FromDateTime(LocalNow);
    public string TodayText => Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public IReadOnlyList<string> AllSlots =>
        Enumerable.Range(_o.OpenHour, _o.CloseHour - _o.OpenHour).Select(HourText).ToList();

    public static bool TryParseDate(string? s, out DateOnly d) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    public static bool TryParseHour(string? s, out int hour)
    {
        hour = -1;
        if (s is null || s.Length != 5 || s[2] != ':' || s[3..] != "00") return false;
        return int.TryParse(s[..2], NumberStyles.None, CultureInfo.InvariantCulture, out hour) && hour is >= 0 and <= 24;
    }

    /// <summary>Validates shape and opening hours. Returns an error code or null.</summary>
    public string? TryBuild(string? date, string? start, string? end, out SlotRange? range)
    {
        range = null;
        if (!TryParseDate(date, out var d)) return "invalid_date";
        if (!TryParseHour(start, out var s) || !TryParseHour(end, out var e)) return "invalid_time";
        if (e <= s) return "end_before_start";
        if (s < _o.OpenHour || e > _o.CloseHour) return "outside_opening_hours";
        range = new SlotRange(d, s, e);
        return null;
    }

    /// <summary>Extra limits for regular users: not in the past, not beyond MaxDaysAhead.</summary>
    public string? CheckUserWindow(SlotRange r)
    {
        var today = Today;
        if (r.Date < today) return "date_in_past";
        if (r.Date > today.AddDays(_o.MaxDaysAhead)) return "too_far_ahead";
        if (r.Date == today && r.StartHour <= LocalNow.Hour) return "time_in_past";
        return null;
    }

    public bool HasEnded(Booking b)
    {
        if (!TryParseDate(b.Date, out var d) || !TryParseHour(b.EndTime, out var e)) return false;
        var end = d.ToDateTime(TimeOnly.MinValue).AddHours(e);
        return end <= LocalNow;
    }

    public string EffectiveStatus(Booking b) =>
        b.Status == BookingStatus.Confirmed && HasEnded(b) ? BookingStatus.Completed : b.Status;
}
