using System.Text.Json;
using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace HourOffice.Api.Mcp;

/// <summary>"Connected apps" for the person, and the admin's MCP switch and connection list (plan 011, S3). These stay
/// available while MCP is switched off, so connections can always be removed.</summary>
public static class ConnectedAppsEndpoints
{
    public sealed record ConnectedAppDto(string ClientId, string Name, bool Verified, List<string> Scopes,
        DateTime? ConnectedAt, DateTime? LastUsedAt);

    public sealed record AdminConnectionDto(string UserId, string Email, string ClientId, string Name, bool Verified,
        List<string> Scopes, DateTime? ConnectedAt, DateTime? LastUsedAt);

    public sealed record McpSettingsDto(bool Enabled, bool SwitchOn, bool Available, string Resource, List<AdminConnectionDto> Connections);

    public sealed record McpSwitchRequest(bool SwitchOn);

    public static void Map(WebApplication app)
    {
        var me = app.MapGroup("/api/me/connected-apps").RequireUser();

        me.MapGet("", async (HttpContext ctx, MongoContext db, IOpenIddictApplicationManager apps, CancellationToken ct) =>
            Results.Ok((await ConnectionsAsync(db, apps, ctx.Me().Id, ct))
                .Select(c => new ConnectedAppDto(c.ClientId, c.Name, c.Verified, c.Scopes, c.ConnectedAt, c.LastUsedAt))));

        me.MapDelete("/{clientId}", async (string clientId, HttpContext ctx, MongoContext db, IOpenIddictApplicationManager apps,
            CancellationToken ct) =>
            await RevokeAsync(db, apps, ctx.Me().Id, clientId, ct) ? Results.NoContent() : Endpoints.Api.NotFound("connection_not_found"));

        var admin = app.MapGroup("/api/admin/settings/mcp").RequireAdmin();

        admin.MapGet("", async (MongoContext db, McpGate gate, IOptions<McpOptions> mcp, IOpenIddictApplicationManager apps,
            CancellationToken ct) =>
        {
            var all = await ConnectionsAsync(db, apps, null, ct);
            var ids = all.Select(c => c.UserId).Distinct().ToList();
            var emails = (await db.Users.Find(u => ids.Contains(u.Id)).ToListAsync(ct)).ToDictionary(u => u.Id, u => u.Email);
            var switchOn = await gate.IsSwitchOnAsync(ct);
            return Results.Ok(new McpSettingsDto(mcp.Value.Enabled, switchOn, mcp.Value.Enabled && switchOn, mcp.Value.Resource,
                all.Select(c => new AdminConnectionDto(c.UserId, emails.GetValueOrDefault(c.UserId, ""), c.ClientId, c.Name,
                    c.Verified, c.Scopes, c.ConnectedAt, c.LastUsedAt)).ToList()));
        });

        admin.MapPut("", async (McpSwitchRequest req, MongoContext db, CancellationToken ct) =>
        {
            await db.Settings.UpdateOneAsync(s => s.Key == SettingKeys.McpEnabled,
                Builders<SiteSetting>.Update.Set(s => s.Value, req.SwitchOn ? "true" : "false")
                    .SetOnInsert(s => s.Id, ObjectId.GenerateNewId().ToString()), new UpdateOptions { IsUpsert = true }, ct);
            return Results.NoContent();
        });

        admin.MapDelete("/connections/{userId}/{clientId}", async (string userId, string clientId, MongoContext db,
            IOpenIddictApplicationManager apps, CancellationToken ct) =>
            Endpoints.Api.IsObjectId(userId) && await RevokeAsync(db, apps, userId, clientId, ct)
                ? Results.NoContent() : Endpoints.Api.NotFound("connection_not_found"));

        admin.MapPost("/revoke-all", async (MongoContext db, CancellationToken ct) =>
        {
            var revoked = await RevokeWhereAsync(db, Builders<BsonDocument>.Filter.Empty, ct);
            return Results.Ok(new { revoked });
        });
    }

    private sealed record Connection(string UserId, string ClientId, string Name, bool Verified, List<string> Scopes,
        DateTime? ConnectedAt, DateTime? LastUsedAt);

    /// <summary>Valid authorizations grouped per person + app; <paramref name="userId"/> null = everyone.</summary>
    private static async Task<List<Connection>> ConnectionsAsync(MongoContext db, IOpenIddictApplicationManager apps,
        string? userId, CancellationToken ct)
    {
        var f = Builders<BsonDocument>.Filter;
        var filter = f.Eq("status", Statuses.Valid);
        if (userId is not null) filter &= f.Eq("subject", userId);
        var docs = await db.Database.GetCollection<BsonDocument>(MongoContext.OAuthAuthorizations).Find(filter)
            .Limit(5000).ToListAsync(ct);
        var result = new List<Connection>();
        foreach (var g in docs.GroupBy(d => (Subject: d.GetValue("subject", "").ToString()!, App: d.GetValue("application_id", BsonNull.Value).ToString()!)))
        {
            var app = await apps.FindByIdAsync(g.Key.App, ct);
            if (app is null) continue;
            var clientId = await apps.GetClientIdAsync(app, ct) ?? "";
            var props = await apps.GetPropertiesAsync(app, ct);
            var verified = props.TryGetValue(OAuthEndpoints.PropVerified, out var v) && v.ValueKind == JsonValueKind.True;
            var scopes = g.SelectMany(d => d.TryGetValue("scopes", out var s) && s.IsBsonArray
                ? s.AsBsonArray.Select(x => x.ToString()!) : []).Distinct().OrderBy(s => McpScopes.All.ToList().IndexOf(s)).ToList();
            // The latest consent: a reconnect moves it, so /ConnectAssistant (plan 012) can tell that it just worked.
            var connectedAt = g.Select(d => d.TryGetValue("creation_date", out var c) && c.IsValidDateTime ? c.ToUniversalTime() : (DateTime?)null)
                .Max();
            var subject = g.Key.Subject;
            var last = await db.McpAudit.Find(a => a.UserId == subject && a.ClientId == clientId)
                .SortByDescending(a => a.At).Project(a => (DateTime?)a.At).FirstOrDefaultAsync(ct);
            result.Add(new Connection(subject, clientId, McpText.Clean(await apps.GetDisplayNameAsync(app, ct), 100), verified,
                scopes, connectedAt, last));
        }
        return result.OrderByDescending(c => c.LastUsedAt ?? c.ConnectedAt).ToList();
    }

    private static async Task<bool> RevokeAsync(MongoContext db, IOpenIddictApplicationManager apps, string userId,
        string clientId, CancellationToken ct)
    {
        var app = await apps.FindByClientIdAsync(clientId, ct);
        if (app is null) return false;
        var appId = await apps.GetIdAsync(app, ct);
        var f = Builders<BsonDocument>.Filter;
        return await RevokeWhereAsync(db, f.Eq("subject", userId) & f.Eq("application_id", ObjectId.Parse(appId)), ct) > 0;
    }

    /// <summary>Marks matching authorizations and their tokens revoked. Token-entry validation makes the very next MCP
    /// call fail (plan 011, Q6).</summary>
    internal static async Task<long> RevokeWhereAsync(MongoContext db, FilterDefinition<BsonDocument> filter, CancellationToken ct)
    {
        var f = Builders<BsonDocument>.Filter;
        var set = Builders<BsonDocument>.Update.Set("status", Statuses.Revoked);
        var auths = await db.Database.GetCollection<BsonDocument>(MongoContext.OAuthAuthorizations)
            .UpdateManyAsync(filter & f.Eq("status", Statuses.Valid), set, cancellationToken: ct);
        await db.Database.GetCollection<BsonDocument>(MongoContext.OAuthTokens)
            .UpdateManyAsync(filter & f.In("status", new[] { Statuses.Valid, Statuses.Inactive }), set, cancellationToken: ct);
        return auths.ModifiedCount;
    }

    /// <summary>When a user is deleted: every MCP connection of theirs ends.</summary>
    public static Task<long> RevokeAllForUserAsync(MongoContext db, string userId, CancellationToken ct) =>
        RevokeWhereAsync(db, Builders<BsonDocument>.Filter.Eq("subject", userId), ct);
}
