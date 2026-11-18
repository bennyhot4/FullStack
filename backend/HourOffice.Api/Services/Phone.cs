using System.Text.RegularExpressions;

namespace HourOffice.Api.Services;

/// <summary>Phone numbers are stored in international E.164 format ("+" country code + number).</summary>
public static partial class Phone
{
    /// <summary>Accepts 05X-XXXXXXX, 5XXXXXXXX, +9725XXXXXXXX etc. Returns +972XXXXXXXXX or null.
    /// Used by the profile and, for Israeli numbers, by <see cref="Normalize"/>.</summary>
    public static string? NormalizeIsraeli(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = NonDigits().Replace(raw, "");
        if (digits.StartsWith("972")) digits = digits[3..];
        digits = digits.TrimStart('0');
        return digits.Length is 8 or 9 ? $"+972{digits}" : null;
    }

    /// <summary>Any country. Input with "+" or "00" is international; country code 972 and input without
    /// "+"/"00" go through <see cref="NormalizeIsraeli"/>. Returns "+" and 8–15 digits, or null.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();
        var digits = NonDigits().Replace(text, "");
        var international = text.StartsWith('+') || digits.StartsWith("00");
        if (!international) return NormalizeIsraeli(text);
        if (!text.StartsWith('+')) digits = digits[2..];
        if (digits.StartsWith("972")) return NormalizeIsraeli(digits);
        return digits.Length is >= 8 and <= 15 && digits[0] != '0' ? $"+{digits}" : null;
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex NonDigits();
}
