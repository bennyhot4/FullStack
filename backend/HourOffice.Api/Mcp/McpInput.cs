using System.Text.RegularExpressions;
using HourOffice.Api.Endpoints;

namespace HourOffice.Api.Mcp;

/// <summary>Strict input checks for tool arguments (plan 011, S6). The shared services check the business rules after.</summary>
public static partial class McpInput
{
    public const int MaxNotes = 500;

    public static void Id(McpCaller caller, string? id, string notFoundCode)
    {
        if (!Endpoints.Api.IsObjectId(id)) throw caller.Fail(notFoundCode, "Unknown id.");
    }

    public static void Date(McpCaller caller, string? date)
    {
        if (date is null || !DateShape().IsMatch(date)) throw caller.Fail("invalid_date", "Dates are yyyy-MM-dd.");
    }

    public static void Time(McpCaller caller, string? time)
    {
        if (time is null || !TimeShape().IsMatch(time)) throw caller.Fail("invalid_time", "Times are HH:00.");
    }

    public static void Notes(McpCaller caller, string? notes)
    {
        if ((notes?.Length ?? 0) > MaxNotes) throw caller.Fail("notes_too_long", $"Notes are at most {MaxNotes} characters.");
    }

    public static void Color(McpCaller caller, string? color)
    {
        if (color is not null && !ColorShape().IsMatch(color)) throw caller.Fail("invalid_color", "Colors are #RRGGBB.");
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DateShape();

    [GeneratedRegex(@"^\d{2}:\d{2}$")]
    private static partial Regex TimeShape();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorShape();
}
