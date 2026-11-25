using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using HourOffice.Api.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace HourOffice.Api.Mcp;

/// <summary>The OAuth 2.1 authorization server for MCP (plan 011, S1/S3): authorization with a consent step on the
/// site, token issuing, and restricted dynamic client registration. OpenIddict validates every protocol detail
/// (PKCE S256, exact redirect URIs, single-use codes, rotating refresh tokens); these handlers only decide who and what.</summary>
public static class OAuthEndpoints
{
    internal const string PropDynamic = "ho_dynamic";
    internal const string PropVerified = "ho_verified";
    internal const string PropConsented = "ho_consented";
    internal const string PropCreated = "ho_created";
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ApprovedLifetime = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan FreshSignIn = TimeSpan.FromMinutes(10);
    /// <summary>admin:write and clients:full last 7 days from the consent, however often the assistant refreshes.</summary>
    internal static readonly TimeSpan SensitiveLifetime = TimeSpan.FromDays(7);
    /// <summary>Private claim (never in the access token) with the end of the sensitive scopes, in seconds since 1970.</summary>
    internal const string SensitiveUntilClaim = "ho_sensitive_until";

    public static void Map(WebApplication app)
    {
        app.MapMethods("/oauth/authorize", ["GET", "POST"], AuthorizeAsync);
        app.MapPost("/oauth/token", TokenAsync);
        app.MapPost("/oauth/register", RegisterAsync).RequireRateLimiting(McpSetup.RegisterPolicy);

        // Called by the consent page with the site's own Bearer token.
        var consent = app.MapGroup("/oauth/authorize").RequireUser();
        consent.MapGet("/request/{id}", RequestInfoAsync);
        consent.MapPost("/decision", DecisionAsync);
    }

    /// <summary>First leg: store the validated request and send the browser to the consent page. Second leg (after the
    /// person approved, with <c>consent=</c>): issue the authorization code.</summary>
    private static async Task<IResult> AuthorizeAsync(HttpContext ctx, MongoContext db, IOptions<McpOptions> mcp,
        TimeProvider clock, IOpenIddictApplicationManager apps, IOpenIddictAuthorizationManager authorizations,
        CancellationToken ct)
    {
        var request = ctx.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Not an OpenIddict request.");
        var now = clock.GetUtcNow().UtcDateTime;
        var consentId = request.GetParameter("consent")?.ToString();

        if (string.IsNullOrEmpty(consentId))
        {
            // OAuth 2.1 makes state optional with PKCE; plan 011 S1 requires it anyway (CSRF on the client side).
            if (string.IsNullOrEmpty(request.State) || request.State.Length > 500)
                return Deny(Errors.InvalidRequest, "The state parameter is required.");
            var requested = request.GetScopes().Where(McpScopes.All.Contains).ToList();
            if (requested.Count == 0) requested = McpScopes.All.Where(McpScopes.DefaultOn.Contains).ToList();
            var doc = new McpConsentRequest
            {
                Id = RandomId(),
                ClientId = request.ClientId ?? "",
                RedirectUri = request.RedirectUri ?? "",
                State = request.State,
                CodeChallenge = request.CodeChallenge ?? "",
                RequestedScopes = requested,
                Parameters = request.GetParameters()
                    .Where(p => p.Key != "consent")
                    .ToDictionary(p => p.Key, p => p.Value.ToString() ?? ""),
                CreatedAt = now,
                ExpiresAt = now.Add(PendingLifetime),
            };
            await db.McpConsents.InsertOneAsync(doc, cancellationToken: ct);
            return Results.Redirect($"{mcp.Value.SiteUrl.TrimEnd('/')}/McpConsent?request={doc.Id}");
        }

        // Single use, bound to the same client, redirect URI and PKCE challenge as the request the person approved.
        var c = await db.McpConsents.FindOneAndUpdateAsync<McpConsentRequest>(
            x => x.Id == consentId && x.Status == McpConsentRequest.Approved && x.ExpiresAt > now
                && x.ClientId == request.ClientId && x.RedirectUri == request.RedirectUri
                && x.CodeChallenge == request.CodeChallenge,
            Builders<McpConsentRequest>.Update.Set(x => x.Status, McpConsentRequest.Used), cancellationToken: ct);
        if (c is null) return Deny(Errors.AccessDenied, "The approval expired or was already used. Connect again.");

        var user = Endpoints.Api.IsObjectId(c.UserId) ? await db.Users.Find(u => u.Id == c.UserId).FirstOrDefaultAsync(ct) : null;
        if (user is not { Status: "active" }) return Deny(Errors.AccessDenied, "The account no longer exists.");
        var allowed = McpScopes.AllowedFor(user).ToHashSet();
        var scopes = c.GrantedScopes.Where(allowed.Contains).ToList();
        if (scopes.Count == 0) return Deny(Errors.AccessDenied, "No permission was granted.");

        var application = await apps.FindByClientIdAsync(c.ClientId, ct)
            ?? throw new InvalidOperationException("The client application disappeared.");
        DateTime? sensitiveUntil = scopes.Any(McpScopes.IsSensitive) ? now.Add(SensitiveLifetime) : null;
        var principal = BuildPrincipal(user, scopes, mcp.Value.Resource, sensitiveUntil, now);
        var authorization = await authorizations.CreateAsync(principal, user.Id,
            await apps.GetIdAsync(application, ct) ?? "", AuthorizationTypes.Permanent, [.. scopes], ct);
        principal.SetAuthorizationId(await authorizations.GetIdAsync(authorization, ct));
        await db.Database.GetCollection<BsonDocument>(MongoContext.OAuthApplications).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("client_id", c.ClientId),
            Builders<BsonDocument>.Update.Set("properties." + PropConsented, true), cancellationToken: ct);
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Code and refresh grants: the person is reloaded, a deleted or demoted one loses access right away.</summary>
    private static async Task<IResult> TokenAsync(HttpContext ctx, MongoContext db, IOptions<McpOptions> mcp, TimeProvider clock,
        CancellationToken ct)
    {
        var request = ctx.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Not an OpenIddict request.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            return Deny(Errors.UnsupportedGrantType, "Only authorization_code and refresh_token.");
        var result = await ctx.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var userId = result.Principal?.GetClaim(Claims.Subject);
        var user = Endpoints.Api.IsObjectId(userId) ? await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct) : null;
        if (user is not { Status: "active" }) return Deny(Errors.InvalidGrant, "The account no longer exists.");

        var now = clock.GetUtcNow().UtcDateTime;
        var allowed = McpScopes.AllowedFor(user).ToHashSet();
        var scopes = result.Principal!.GetScopes().Where(allowed.Contains).ToList();
        // The sensitive scopes end 7 days after the consent and a refresh never extends them (the refresh token itself
        // expires then too, so the assistant has to connect again with a fresh sign-in).
        var sensitiveUntil = long.TryParse(result.Principal!.GetClaim(SensitiveUntilClaim), out var su)
            ? DateTimeOffset.FromUnixTimeSeconds(su).UtcDateTime : (DateTime?)null;
        if (sensitiveUntil is null || sensitiveUntil <= now) scopes.RemoveAll(McpScopes.IsSensitive);
        if (scopes.Count == 0) return Deny(Errors.InvalidGrant, "No permission is left.");
        var principal = BuildPrincipal(user, scopes, mcp.Value.Resource, sensitiveUntil, now);
        principal.SetAuthorizationId(result.Principal!.GetAuthorizationId());
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Only the person's id goes into the (encrypted) token; roles are always read from the DB.</summary>
    private static ClaimsPrincipal BuildPrincipal(User user, IReadOnlyCollection<string> scopes, string resource,
        DateTime? sensitiveUntil, DateTime now)
    {
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id);
        var principal = new ClaimsPrincipal(identity);
        // offline_access: OpenIddict issues a (rotating) refresh token only with it. It is not a permission.
        principal.SetScopes(scopes.Append(Scopes.OfflineAccess));
        principal.SetResources(resource);
        if (scopes.Any(McpScopes.IsSensitive) && sensitiveUntil is { } until)
        {
            // Kept in the code and refresh token only (no destination); the refresh token dies with the sensitive scopes.
            identity.SetClaim(SensitiveUntilClaim, new DateTimeOffset(until, TimeSpan.Zero).ToUnixTimeSeconds());
            principal.SetRefreshTokenLifetime(until - now);
        }
        principal.SetDestinations(claim => claim.Type == Claims.Subject ? [Destinations.AccessToken] : []);
        return principal;
    }

    private static IResult Deny(string error, string description) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    public sealed record ConsentScopeDto(string Scope, bool DefaultOn, bool Sensitive, bool Admin);

    public sealed record ConsentRequestDto(string RequestId, string ClientName, string RedirectHost, bool Verified,
        List<ConsentScopeDto> Scopes, bool NeedsFreshSignIn, DateTime ExpiresAt);

    public sealed record DecisionRequest(string? RequestId, bool Approve, List<string>? Scopes);

    public sealed record DecisionResponse(string RedirectUrl);

    private static async Task<IResult> RequestInfoAsync(string id, HttpContext ctx, MongoContext db, TimeProvider clock,
        IOpenIddictApplicationManager apps, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var c = await db.McpConsents.Find(x => x.Id == id && x.Status == McpConsentRequest.Pending && x.ExpiresAt > now)
            .FirstOrDefaultAsync(ct);
        if (c is null) return Endpoints.Api.NotFound("consent_not_found");
        var app = await apps.FindByClientIdAsync(c.ClientId, ct);
        if (app is null) return Endpoints.Api.NotFound("consent_not_found");
        var user = ctx.Me();
        var allowed = McpScopes.AllowedFor(user).ToHashSet();
        var scopes = c.RequestedScopes.Where(allowed.Contains)
            .Select(s => new ConsentScopeDto(s, McpScopes.DefaultOn.Contains(s), McpScopes.IsSensitive(s), McpScopes.IsAdmin(s)))
            .ToList();
        var props = await apps.GetPropertiesAsync(app, ct);
        var verified = props.TryGetValue(PropVerified, out var v) && v.ValueKind == JsonValueKind.True;
        var host = Uri.TryCreate(c.RedirectUri, UriKind.Absolute, out var u) ? u.Authority : "";
        return Results.Ok(new ConsentRequestDto(c.Id, McpText.Clean(await apps.GetDisplayNameAsync(app, ct), 100), host,
            verified, scopes, !IsFresh(ctx, now), c.ExpiresAt));
    }

    private static async Task<IResult> DecisionAsync(DecisionRequest req, HttpContext ctx, MongoContext db,
        IOptions<McpOptions> mcp, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var c = string.IsNullOrEmpty(req.RequestId) ? null : await db.McpConsents
            .Find(x => x.Id == req.RequestId && x.Status == McpConsentRequest.Pending && x.ExpiresAt > now).FirstOrDefaultAsync(ct);
        if (c is null) return Endpoints.Api.NotFound("consent_not_found");
        var user = ctx.Me();
        var f = Builders<McpConsentRequest>.Filter;
        var pending = f.Eq(x => x.Id, c.Id) & f.Eq(x => x.Status, McpConsentRequest.Pending);

        if (!req.Approve)
        {
            await db.McpConsents.UpdateOneAsync(pending, Builders<McpConsentRequest>.Update
                .Set(x => x.Status, McpConsentRequest.Denied).Set(x => x.UserId, user.Id), cancellationToken: ct);
            var query = new Dictionary<string, string?> { ["error"] = Errors.AccessDenied, ["iss"] = mcp.Value.Issuer };
            if (c.State is not null) query["state"] = c.State;
            return Results.Ok(new DecisionResponse(QueryHelpers.AddQueryString(c.RedirectUri, query)));
        }

        var allowed = McpScopes.AllowedFor(user).ToHashSet();
        var granted = (req.Scopes ?? new()).Distinct().Where(s => c.RequestedScopes.Contains(s) && allowed.Contains(s)).ToList();
        if (granted.Count == 0) return Endpoints.Api.BadRequest("no_scopes", "Choose at least one permission.");
        if (granted.Any(McpScopes.IsSensitive) && !IsFresh(ctx, now))
            return Endpoints.Api.Forbidden("reauth_required");

        var res = await db.McpConsents.UpdateOneAsync(pending, Builders<McpConsentRequest>.Update
            .Set(x => x.Status, McpConsentRequest.Approved).Set(x => x.UserId, user.Id)
            .Set(x => x.GrantedScopes, granted).Set(x => x.ExpiresAt, now.Add(ApprovedLifetime)), cancellationToken: ct);
        if (res.ModifiedCount == 0) return Endpoints.Api.NotFound("consent_not_found");
        var replay = c.Parameters.ToDictionary(p => p.Key, p => (string?)p.Value);
        replay["consent"] = c.Id;
        return Results.Ok(new DecisionResponse(QueryHelpers.AddQueryString(mcp.Value.Issuer.TrimEnd('/') + "/oauth/authorize", replay)));
    }

    /// <summary>Sensitive permissions need a Google sign-in within the last 10 minutes by this very session (the
    /// <c>auth_time</c> of the site token), not just any recent sign-in of the account.</summary>
    private static bool IsFresh(HttpContext ctx, DateTime now) =>
        TokenService.AuthTimeOf(ctx.User) is { } t && now - t <= FreshSignIn && t <= now.AddMinutes(1);

    public sealed record RegistrationRequest(
        [property: JsonPropertyName("client_name")] string? ClientName,
        [property: JsonPropertyName("redirect_uris")] List<string>? RedirectUris,
        [property: JsonPropertyName("grant_types")] List<string>? GrantTypes,
        [property: JsonPropertyName("response_types")] List<string>? ResponseTypes,
        [property: JsonPropertyName("token_endpoint_auth_method")] string? TokenEndpointAuthMethod,
        [property: JsonPropertyName("scope")] string? Scope);

    public sealed record RegistrationResponse(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_id_issued_at")] long ClientIdIssuedAt,
        [property: JsonPropertyName("client_name")] string ClientName,
        [property: JsonPropertyName("redirect_uris")] List<string> RedirectUris,
        [property: JsonPropertyName("grant_types")] string[] GrantTypes,
        [property: JsonPropertyName("response_types")] string[] ResponseTypes,
        [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("client_secret"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClientSecret = null,
        [property: JsonPropertyName("client_secret_expires_at"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ClientSecretExpiresAt = null);

    /// <summary>Token endpoint authentication methods a registering client may ask for. Assistants that run on a
    /// server (ChatGPT) ask for a client secret; desktop apps use none. Either way PKCE S256 is still required.</summary>
    private static readonly string[] AuthMethods = ["none", "client_secret_post", "client_secret_basic"];

    /// <summary>RFC 7591, restricted (S7): public clients, or confidential ones that get a server-generated secret; at most
    /// 5 exact redirect URIs (https, or http on loopback). Never-used registrations are removed after 24 hours.</summary>
    private static async Task<IResult> RegisterAsync(HttpContext ctx, MongoContext db, IOptions<McpOptions> mcp,
        TimeProvider clock, IOpenIddictApplicationManager apps, ILoggerFactory logs, CancellationToken ct)
    {
        RegistrationRequest? req;
        try
        {
            req = await ctx.Request.ReadFromJsonAsync<RegistrationRequest>(ct);
        }
        catch (JsonException)
        {
            return Refuse(null, "invalid_client_metadata", "The body is not valid JSON.");
        }
        if (req is null) return Refuse(null, "invalid_client_metadata", "The body is empty.");
        var method = req.TokenEndpointAuthMethod ?? "none";
        if (!AuthMethods.Contains(method))
            return Refuse(req, "invalid_client_metadata", "token_endpoint_auth_method must be none, client_secret_post or client_secret_basic.");
        if (req.GrantTypes?.Any(g => g is not (GrantTypes.AuthorizationCode or GrantTypes.RefreshToken)) == true)
            return Refuse(req, "invalid_client_metadata", "Only authorization_code and refresh_token.");
        if (req.ResponseTypes?.Any(r => r != ResponseTypes.Code) == true)
            return Refuse(req, "invalid_client_metadata", "Only the code response type.");
        var uris = (req.RedirectUris ?? new()).Distinct(StringComparer.Ordinal).ToList();
        if (uris.Count is 0 or > 5) return Refuse(req, "invalid_redirect_uri", "Give 1 to 5 redirect URIs.");
        foreach (var uri in uris)
            if (!IsAllowedRedirect(uri)) return Refuse(req, "invalid_redirect_uri", "Redirect URIs must be https (or http on loopback), without a fragment.");
        var name = McpText.Clean(req.ClientName, 100);
        if ((req.ClientName?.Length ?? 0) > 100) return Refuse(req, "invalid_client_metadata", "client_name is at most 100 characters.");

        var now = clock.GetUtcNow().UtcDateTime;
        await CleanupUnusedAsync(db, now, ct);

        var known = mcp.Value.KnownClients.FirstOrDefault(k => uris.All(u => k.RedirectUris.Contains(u, StringComparer.Ordinal)));
        // A confidential client gets a random secret, shown once in this response; OpenIddict stores only its hash.
        var secret = method == "none" ? null : RandomId();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = RandomId(),
            ClientSecret = secret,
            ClientType = secret is null ? ClientTypes.Public : ClientTypes.Confidential,
            ApplicationType = ApplicationTypes.Web,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = known?.Name ?? (name.Length > 0 ? name : "Unnamed app"),
        };
        foreach (var uri in uris) descriptor.RedirectUris.Add(new Uri(uri));
        descriptor.Permissions.UnionWith(
        [
            Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Resource + mcp.Value.Resource,
        ]);
        foreach (var s in McpScopes.All.Append(Scopes.OfflineAccess)) descriptor.Permissions.Add(Permissions.Prefixes.Scope + s);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        descriptor.Properties[PropDynamic] = JsonSerializer.SerializeToElement(true);
        descriptor.Properties[PropVerified] = JsonSerializer.SerializeToElement(known is not null);
        descriptor.Properties[PropConsented] = JsonSerializer.SerializeToElement(false);
        descriptor.Properties[PropCreated] = JsonSerializer.SerializeToElement(now.ToString("O"));
        await apps.CreateAsync(descriptor, ct);

        return Results.Json(new RegistrationResponse(descriptor.ClientId!, new DateTimeOffset(now).ToUnixTimeSeconds(),
            descriptor.DisplayName!, uris, [GrantTypes.AuthorizationCode, GrantTypes.RefreshToken], [ResponseTypes.Code],
            method, string.Join(' ', McpScopes.All), secret, secret is null ? null : 0), statusCode: StatusCodes.Status201Created);

        // Every refusal is logged with what the client asked for (never a secret: a registration request has none), so a
        // client that cannot connect can be diagnosed from the logs.
        IResult Refuse(RegistrationRequest? r, string error, string description)
        {
            logs.CreateLogger("HourOffice.Mcp").LogWarning(
                "MCP client registration refused: {Error} ({Description}). Client {ClientName}, auth method {AuthMethod}, " +
                "grant types {GrantTypes}, response types {ResponseTypes}, redirect URIs {RedirectUris}",
                error, description, McpText.Clean(r?.ClientName, 100), McpText.Clean(r?.TokenEndpointAuthMethod, 40),
                McpText.Clean(string.Join(',', r?.GrantTypes ?? []), 200), McpText.Clean(string.Join(',', r?.ResponseTypes ?? []), 100),
                McpText.Clean(string.Join(' ', r?.RedirectUris ?? []), 600));
            return RegistrationError(error, description);
        }
    }

    internal static bool IsAllowedRedirect(string uri)
    {
        if (uri.Length > 2000 || !Uri.TryCreate(uri, UriKind.Absolute, out var u) || u.Fragment.Length > 0
            || !string.IsNullOrEmpty(u.UserInfo)) return false;
        if (u.Scheme == Uri.UriSchemeHttps) return true;
        return u.Scheme == Uri.UriSchemeHttp && (u.IsLoopback || u.Host == "localhost");
    }

    private static async Task CleanupUnusedAsync(MongoContext db, DateTime now, CancellationToken ct)
    {
        var f = Builders<BsonDocument>.Filter;
        await db.Database.GetCollection<BsonDocument>(MongoContext.OAuthApplications).DeleteManyAsync(
            f.Eq("properties." + PropDynamic, true) & f.Ne("properties." + PropConsented, true)
            & f.Lt("properties." + PropCreated, now.AddHours(-24).ToString("O")), ct);
    }

    private static IResult RegistrationError(string error, string description) =>
        Results.Json(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description }, statusCode: 400);

    private static string RandomId() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
}
