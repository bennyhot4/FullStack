using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using HourOffice.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using MongoDB.Driver;
using OpenIddict.Abstractions;

namespace HourOffice.Api.Mcp;

/// <summary>A tool needs this scope in the token and, for admin scopes, the admin role read fresh from the DB.</summary>
public sealed record McpScopeRequirement(string Scope) : IAuthorizationRequirement;

/// <summary>Every /mcp request: the token's user still exists and is active.</summary>
public sealed record McpActiveUserRequirement : IAuthorizationRequirement;

public sealed class McpScopeHandler(IHttpContextAccessor http, MongoContext db) : AuthorizationHandler<McpScopeRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, McpScopeRequirement requirement)
    {
        if (!context.User.HasScope(requirement.Scope) || http.HttpContext is not { } ctx) return;
        var user = await ctx.CurrentUserAsync(db);
        if (user is not { Status: "active" }) return;
        if (McpScopes.IsAdmin(requirement.Scope) && user.Role != Roles.Admin) return;
        context.Succeed(requirement);
    }
}

public sealed class McpActiveUserHandler(IHttpContextAccessor http, MongoContext db) : AuthorizationHandler<McpActiveUserRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, McpActiveUserRequirement requirement)
    {
        if (http.HttpContext is not { } ctx) return;
        if (await ctx.CurrentUserAsync(db) is { Status: "active" }) context.Succeed(requirement);
    }
}

/// <summary>The kill switch: the env var <c>Mcp__Enabled</c> is the master, the admin switch can turn it off.</summary>
public sealed class McpGate(IOptions<McpOptions> options, MongoContext db)
{
    public async Task<bool> IsEnabledAsync(CancellationToken ct) =>
        options.Value.Enabled && await IsSwitchOnAsync(ct);

    public async Task<bool> IsSwitchOnAsync(CancellationToken ct) =>
        (await db.Settings.Find(s => s.Key == SettingKeys.McpEnabled).FirstOrDefaultAsync(ct))?.Value != "false";
}

/// <summary>A tool error the assistant sees as <c>code: message</c>. The code is also kept for the audit log.</summary>
public sealed class McpToolError(string code, string message) : McpException($"{code}: {message}")
{
    public string Code { get; } = code;
}

/// <summary>The caller of the current tool: who they are, what they granted, and the per-call helpers
/// (scope checks, confirmation, per-hour limits). One instance per request.</summary>
public sealed class McpCaller(IHttpContextAccessor http, MongoContext db, IOptions<McpOptions> options, TimeProvider clock)
{
    internal const string TargetKey = "mcp.target";
    internal const string ErrorKey = "mcp.error";
    internal const string WriteKey = "mcp.write";
    internal const string ToolKey = "mcp.tool";
    internal const string ReservationKey = "mcp.reservation";
    internal const string Reserved = "reserved";

    private HttpContext Ctx => http.HttpContext ?? throw new InvalidOperationException("No HTTP context.");

    public string ClientId => Ctx.User.GetPresenters().FirstOrDefault()
        ?? Ctx.User.GetClaim(OpenIddictConstants.Claims.ClientId) ?? "";

    public HttpRequest Request => Ctx.Request;

    public bool Has(string scope) => Ctx.User.HasScope(scope);

    /// <summary>The signed-in, active user holding every one of <paramref name="scopes"/> (admin scopes: an admin).
    /// The tools are also hidden by <c>[Authorize]</c>; this is the second, explicit check.</summary>
    public async Task<User> RequireAsync(CancellationToken ct, params string[] scopes)
    {
        foreach (var s in scopes)
            if (!Has(s)) throw Fail("insufficient_scope", $"This needs the '{s}' permission. Reconnect the app and allow it.");
        var user = await Ctx.CurrentUserAsync(db);
        if (user is not { Status: "active" }) throw Fail("unauthorized", "The account no longer exists.");
        if (scopes.Any(McpScopes.IsAdmin) && user.Role != Roles.Admin) throw Fail("admin_required", "Only admins can do this.");
        _ = ct;
        return user;
    }

    /// <summary>A business-rule failure from a shared service, as a tool error with the REST error code.</summary>
    public McpToolError Fail(ServiceError e) => Fail(e.Code, e.Message ?? e.Code);

    public McpToolError Fail(string code, string message)
    {
        Ctx.Items[ErrorKey] = code;
        return new McpToolError(code, message);
    }

    public T Ok<T>(Outcome<T> outcome) => outcome.Error is { } e ? throw Fail(e) : outcome.Value!;

    /// <summary>Marks the call as a write and records what it changed (for the audit log).</summary>
    public void Changed(string? targetId)
    {
        Ctx.Items[WriteKey] = true;
        if (targetId is not null) Ctx.Items[TargetKey] = targetId;
    }

    /// <summary>Per-hour limits for writes through MCP (plan 011, S5), counted from the audit log so they hold
    /// across instances. The call first reserves its row, then counts: of any calls running at the same time, the
    /// last to count sees all of them, so parallel calls cannot get past the limit together. The filter turns the
    /// reservation into the final audit row; a failed or declined call does not use up the limit.</summary>
    public async Task CheckHourlyLimitAsync(User user, string[] tools, int limit, CancellationToken ct)
    {
        var tool = Ctx.Items[ToolKey] as string ?? "";
        var now = clock.GetUtcNow().UtcDateTime;
        var reservation = new McpAuditEntry
        {
            At = now, UserId = user.Id, Role = user.Role, Tool = tool, Outcome = Reserved, IsWrite = true,
            ExpiresAt = now.AddDays(1),
        };
        await db.McpAudit.InsertOneAsync(reservation, cancellationToken: ct);
        var since = now.AddHours(-1);
        var used = await db.McpAudit.CountDocumentsAsync(a => a.UserId == user.Id && tools.Contains(a.Tool)
            && (a.Outcome == "ok" || a.Outcome == Reserved) && a.At >= since, cancellationToken: ct);
        if (used > limit)
        {
            await db.McpAudit.DeleteOneAsync(a => a.Id == reservation.Id, CancellationToken.None);
            throw Fail("rate_limited", $"At most {limit} of these changes per hour through an assistant. Try later or use the site.");
        }
        Ctx.Items[ReservationKey] = reservation.Id;
    }

    public McpOptions Options => options.Value;

    public MongoContext Db => db;

    /// <summary>Asks the person to confirm a change, when the assistant supports it: MRTR (stateless input requests)
    /// first, then classic elicitation. Without support the change goes ahead, and the tool result says what was done.
    /// Returns false when the person declined.</summary>
    public async Task<bool> ConfirmAsync(RequestContext<CallToolRequestParams> rc, string message, CancellationToken ct)
    {
        const string key = "confirm";
        if (rc.Params?.InputResponses is { } responses && responses.TryGetValue(key, out var response))
        {
            var result = response.RawValue.Deserialize<ElicitResult>(McpJsonUtilities.DefaultOptions);
            return result is { IsAccepted: true } && result.Content is { } c
                && c.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.True;
        }

        var server = rc.Server;
        var request = new ElicitRequestParams
        {
            Message = message,
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    [key] = new ElicitRequestParams.BooleanSchema { Title = "Confirm", Description = "Do it", Default = false },
                },
                Required = [key],
            },
        };
        if (server.IsMrtrSupported)
            throw new InputRequiredException(new Dictionary<string, InputRequest> { [key] = InputRequest.ForElicitation(request) }, null);
        if (server.ClientCapabilities?.Elicitation is null) return true;
        try
        {
            var result = await server.ElicitAsync(request, ct);
            return result is { IsAccepted: true } && result.Content is { } c
                && c.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.True;
        }
        catch (InvalidOperationException)
        {
            // Stateless transport without MRTR cannot ask; continue as if the client had no elicitation.
            return true;
        }
    }

    public McpToolError Declined()
    {
        Ctx.Items[ErrorKey] = "declined";
        return new McpToolError("declined", "The person did not confirm. Nothing was changed.");
    }

    /// <summary>Keyed hash (HMAC) of the IP: a plain SHA-256 of an IPv4 address is reversed by trying all 2^32.
    /// The key is the server's secret JWT signing key, so the hash cannot be recomputed from a database dump.</summary>
    internal static string HashIp(string? ip, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("mcp-ip:" + secret),
            Encoding.UTF8.GetBytes(ip ?? "unknown")))[..16];
}
