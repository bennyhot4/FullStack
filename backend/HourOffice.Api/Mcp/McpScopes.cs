namespace HourOffice.Api.Mcp;

/// <summary>The permissions a person grants an AI assistant on the consent page (plan 011, "Scopes").</summary>
public static class McpScopes
{
    public const string OfficesRead = "offices:read";
    public const string BookingsRead = "bookings:read";
    public const string BookingsWrite = "bookings:write";
    public const string ClientsBasic = "clients:basic";
    public const string ClientsFull = "clients:full";
    public const string AdminRead = "admin:read";
    public const string AdminWrite = "admin:write";

    public static readonly IReadOnlyList<string> All =
        [OfficesRead, BookingsRead, BookingsWrite, ClientsBasic, ClientsFull, AdminRead, AdminWrite];

    /// <summary>Checked by default on the consent page.</summary>
    public static readonly IReadOnlySet<string> DefaultOn = new HashSet<string> { OfficesRead, BookingsRead, BookingsWrite, ClientsBasic };

    public static bool IsAdmin(string scope) => scope is AdminRead or AdminWrite;

    /// <summary>Need a Google sign-in within the last 10 minutes and get a 7-day refresh token.</summary>
    public static bool IsSensitive(string scope) => scope is AdminWrite or ClientsFull;

    /// <summary>The scopes a person may grant: admin scopes only for admins.</summary>
    public static IEnumerable<string> AllowedFor(User user) =>
        All.Where(s => !IsAdmin(s) || user.Role == Roles.Admin);

    /// <summary>Authorization policy name used on the tools (<c>[Authorize(Policy = ...)]</c>).</summary>
    public static string Policy(string scope) => "mcp:" + scope;
}
