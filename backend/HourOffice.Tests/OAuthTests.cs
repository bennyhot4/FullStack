using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HourOffice.Api;
using HourOffice.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HourOffice.Tests;

/// <summary>Plan 011, S1/S3/S7: the OAuth 2.1 authorization server and access control of /mcp.</summary>
[Collection("api")]
public sealed class OAuthTests(MongoFixture mongo) : IDisposable
{
    private readonly TestApp _app = new(mongo, new() { ["Mcp:Enabled"] = "true", ["Mcp:AllowedOrigins:0"] = "https://claude.ai",
        ["Mcp:KnownClients:0:Name"] = "Claude", ["Mcp:KnownClients:0:RedirectUris:0"] = "https://claude.ai/api/mcp/auth_callback" });
    public void Dispose() => _app.Dispose();

    private McpHarness H() => new(_app);

    [Fact]
    public async Task Mcp_without_a_token_is_401_with_resource_metadata()
    {
        var (res, _) = await H().RpcAsync(null, "tools/list");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var header = res.Headers.WwwAuthenticate.ToString();
        Assert.Contains("resource_metadata=", header);
        Assert.Contains("/.well-known/oauth-protected-resource", header);
    }

    [Fact]
    public async Task A_burst_of_401s_from_one_ip_is_flagged_once_per_minute()
    {
        // 19 failed calls through the real pipeline; the 20th (counted directly) crosses the threshold, later ones do not.
        for (var i = 1; i < HourOffice.Api.Mcp.McpAuthFailureMonitor.Threshold; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await H().RpcAsync(null, "tools/list")).Item1.StatusCode);
        var monitor = _app.Services.GetRequiredService<HourOffice.Api.Mcp.McpAuthFailureMonitor>();
        // TestServer has no remote IP, like this bare context: both count as "unknown".
        Assert.True(monitor.Record(new Microsoft.AspNetCore.Http.DefaultHttpContext()));
        Assert.False(monitor.Record(new Microsoft.AspNetCore.Http.DefaultHttpContext()));
        _app.Clock.Now = _app.Clock.Now.AddMinutes(1);
        Assert.False(monitor.Record(new Microsoft.AspNetCore.Http.DefaultHttpContext()));
    }

    [Fact]
    public async Task Discovery_documents_describe_the_resource_and_the_server()
    {
        var h = H();
        var prm = await (await h.Raw.GetAsync("/.well-known/oauth-protected-resource")).Json();
        Assert.Equal(McpHarness.Resource, prm.GetProperty("resource").GetString());
        Assert.Contains("http://localhost/", prm.GetProperty("authorization_servers").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(7, prm.GetProperty("scopes_supported").GetArrayLength());

        var asm = await (await h.Raw.GetAsync("/.well-known/oauth-authorization-server")).Json();
        Assert.Equal("http://localhost/", asm.GetProperty("issuer").GetString());
        Assert.Equal("http://localhost/oauth/register", asm.GetProperty("registration_endpoint").GetString());
        Assert.Equal(["S256"], asm.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(x => x.GetString()!));
        Assert.DoesNotContain("password", asm.GetProperty("grant_types_supported").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Tools_follow_role_and_granted_scopes()
    {
        var h = H();
        var user = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var all = await h.ConnectAsync(user, grant: McpHarness.AllScopes);
        var userTools = await h.ToolNamesAsync(all.Access);
        Assert.Equal(8, userTools.Count);
        Assert.DoesNotContain(userTools, t => t.StartsWith("admin_"));

        var narrow = await h.ConnectAsync(user, grant: ["offices:read"]);
        Assert.Equal(["get_booking_rules", "get_office_availability", "list_offices"], (await h.ToolNamesAsync(narrow.Access)).Order());

        var admin = await _app.AdminAsync();
        var adminAll = await h.ConnectAsync(admin, grant: McpHarness.AllScopes);
        Assert.Equal(22, (await h.ToolNamesAsync(adminAll.Access)).Count);
    }

    [Fact]
    public async Task A_user_cannot_grant_admin_scopes()
    {
        var h = H();
        var user = await _app.LoginAsync("dana@example.com");
        var clientId = await h.RegisterAsync();
        var (verifier, challenge) = McpHarness.Pkce();
        var id = await h.StartAsync(clientId, challenge, McpHarness.AllScopes);

        var info = await (await user.GetAsync($"/oauth/authorize/request/{id}")).Json();
        var offered = info.GetProperty("scopes").EnumerateArray().Select(s => s.GetProperty("scope").GetString()).ToList();
        Assert.DoesNotContain("admin:read", offered);
        Assert.DoesNotContain("admin:write", offered);
        Assert.Equal("127.0.0.1:7777", info.GetProperty("redirectHost").GetString());
        Assert.False(info.GetProperty("verified").GetBoolean());

        var decision = await McpHarness.DecideAsync(user, id, true, ["admin:write", "offices:read"]);
        var code = await h.CodeAsync((await decision.Json()).GetProperty("redirectUrl").GetString()!);
        var token = await (await h.TokenAsync(clientId, code, verifier)).Json();
        Assert.Equal("offices:read offline_access", token.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Sensitive_scopes_need_a_fresh_google_sign_in()
    {
        var h = H();
        var admin = await _app.AdminAsync();
        _app.Clock.Now = _app.Clock.Now.AddMinutes(11);
        var clientId = await h.RegisterAsync();
        var (_, challenge) = McpHarness.Pkce();
        var id = await h.StartAsync(clientId, challenge, McpHarness.AllScopes);
        Assert.True((await (await admin.GetAsync($"/oauth/authorize/request/{id}")).Json()).GetProperty("needsFreshSignIn").GetBoolean());
        var stale = await McpHarness.DecideAsync(admin, id, true, ["admin:write"]);
        Assert.Equal(HttpStatusCode.Forbidden, stale.StatusCode);
        Assert.Equal("reauth_required", await stale.Code());
        // Non-sensitive scopes are fine with an older sign-in.
        Assert.Equal(HttpStatusCode.OK, (await McpHarness.DecideAsync(admin, id, true, ["admin:read"])).StatusCode);
    }

    [Fact]
    public async Task Pkce_s256_and_registered_redirects_are_required()
    {
        var h = H();
        var clientId = await h.RegisterAsync();
        var (_, challenge) = McpHarness.Pkce();

        var noPkce = await h.Raw.GetAsync(McpHarness.AuthorizeUrl(clientId, challenge, ["offices:read"]).Replace("code_challenge=", "x="));
        Assert.Contains("error=", noPkce.Headers.Location?.ToString() ?? "error=");

        var plain = await h.Raw.GetAsync(McpHarness.AuthorizeUrl(clientId, challenge, ["offices:read"], method: "plain"));
        Assert.Contains("error=", plain.Headers.Location?.ToString() ?? "error=");
        Assert.DoesNotContain("McpConsent", plain.Headers.Location?.ToString() ?? "");

        var wrongRedirect = await h.Raw.GetAsync(McpHarness.AuthorizeUrl(clientId, challenge, ["offices:read"],
            redirect: "https://evil.example/cb"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongRedirect.StatusCode);
    }

    [Fact]
    public async Task Codes_are_single_use_and_refresh_reuse_revokes_the_chain()
    {
        var h = H();
        var user = await _app.LoginAsync("dana@example.com");
        var clientId = await h.RegisterAsync();
        var (verifier, challenge) = McpHarness.Pkce();
        var id = await h.StartAsync(clientId, challenge, McpHarness.UserScopes);
        var redirect = (await (await McpHarness.DecideAsync(user, id, true, McpHarness.UserScopes)).Json())
            .GetProperty("redirectUrl").GetString()!;
        var code = await h.CodeAsync(redirect);
        Assert.Equal(HttpStatusCode.OK, (await h.TokenAsync(clientId, code, verifier)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.TokenAsync(clientId, code, verifier)).StatusCode);
        // The approval link itself is single use too.
        var again = await h.Raw.GetAsync(new Uri(redirect).PathAndQuery);
        Assert.Contains("error=access_denied", again.Headers.Location!.ToString());

        var t = await h.ConnectAsync(user, McpHarness.UserScopes);
        var first = await (await h.RefreshAsync(t.ClientId, t.Refresh)).Json();
        var rotated = first.GetProperty("refresh_token").GetString()!;
        Assert.NotEqual(t.Refresh, rotated);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.RefreshAsync(t.ClientId, t.Refresh)).StatusCode);
        // Reuse revoked the whole chain: the rotated token and the newest access token stop working.
        Assert.Equal(HttpStatusCode.BadRequest, (await h.RefreshAsync(t.ClientId, rotated)).StatusCode);
        var (res, _) = await h.RpcAsync(first.GetProperty("access_token").GetString(), "tools/list");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_tokens_are_shorter_for_sensitive_scopes()
    {
        // MongoDB's TTL monitor uses the real clock: keep the fake clock near it so the 7-day token is not deleted.
        _app.Clock.Now = DateTimeOffset.UtcNow;
        var h = H();
        var admin = await _app.AdminAsync();
        var db = _app.Services.GetRequiredService<MongoContext>().Database.GetCollection<BsonDocument>(MongoContext.OAuthTokens);
        var plain = await h.ConnectAsync(admin, McpHarness.UserScopes);
        var sensitive = await h.ConnectAsync(admin, McpHarness.AllScopes);

        async Task<double> DaysAsync(string clientId)
        {
            var app = await _app.Services.GetRequiredService<MongoContext>().Database
                .GetCollection<BsonDocument>(MongoContext.OAuthApplications).Find(new BsonDocument("client_id", clientId)).FirstAsync();
            var all = await db.Find(new BsonDocument("application_id", app["_id"])).ToListAsync();
            var tok = all.FirstOrDefault(t => t.GetValue("type", "").ToString()!.EndsWith(":refresh_token") && t.GetValue("status", "").ToString() == "valid")
                ?? throw new Exception(string.Join(" | ", all.Select(t => $"{t.GetValue("type", "-")}/{t.GetValue("status", "-")}/{t.GetValue("expiration_date", "-")}/{string.Join(",", t.Names)}")));
            return (tok["expiration_date"].ToUniversalTime() - tok["creation_date"].ToUniversalTime()).TotalDays;
        }
        Assert.Equal(30, await DaysAsync(plain.ClientId), 1);
        Assert.Equal(7, await DaysAsync(sensitive.ClientId), 1);
    }

    [Fact]
    public async Task Site_tokens_and_mcp_tokens_are_not_interchangeable()
    {
        var h = H();
        var user = await _app.LoginAsync("dana@example.com");
        var siteToken = user.DefaultRequestHeaders.Authorization!.Parameter!;
        var (res, _) = await h.RpcAsync(siteToken, "tools/list");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

        var t = await h.ConnectAsync(user, McpHarness.UserScopes);
        var c = _app.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t.Access);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/bookings/mine")).StatusCode);
    }

    [Fact]
    public async Task Disconnect_revoke_all_and_user_delete_end_access_on_the_next_call()
    {
        var h = H();
        var user = await _app.LoginAsync("dana@example.com");
        var t = await h.ConnectAsync(user, McpHarness.UserScopes);
        Assert.Equal(8, (await h.ToolNamesAsync(t.Access)).Count);

        var apps = await (await user.GetAsync("/api/me/connected-apps")).Json();
        Assert.Equal(t.ClientId, apps[0].GetProperty("clientId").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/me/connected-apps/{t.ClientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.RpcAsync(t.Access, "tools/list")).Http.StatusCode);
        Assert.Equal(0, (await (await user.GetAsync("/api/me/connected-apps")).Json()).GetArrayLength());

        var admin = await _app.AdminAsync();
        var t2 = await h.ConnectAsync(user, McpHarness.UserScopes);
        var adminView = await (await admin.GetAsync("/api/admin/settings/mcp")).Json();
        Assert.Equal("dana@example.com", adminView.GetProperty("connections")[0].GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/admin/settings/mcp/revoke-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await h.RpcAsync(t2.Access, "tools/list")).Http.StatusCode);

        var t3 = await h.ConnectAsync(user, McpHarness.UserScopes);
        var me = await (await user.GetAsync("/api/me")).Json();
        await admin.DeleteAsync($"/api/admin/users/{me.GetProperty("user").GetProperty("id").GetString()}");
        Assert.NotEqual(HttpStatusCode.OK, (await h.RpcAsync(t3.Access, "tools/list")).Http.StatusCode);
    }

    [Fact]
    public async Task Kill_switch_turns_mcp_and_oauth_off()
    {
        var h = H();
        var admin = await _app.AdminAsync();
        var t = await h.ConnectAsync(admin, McpHarness.UserScopes);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/admin/settings/mcp", new { switchOn = false })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.RpcAsync(t.Access, "tools/list")).Http.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Raw.GetAsync("/oauth/authorize?client_id=x")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await h.Raw.GetAsync("/.well-known/oauth-authorization-server")).StatusCode);
        // Connected apps can still be listed (and removed) while switched off.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/me/connected-apps")).StatusCode);
        await admin.PutAsJsonAsync("/api/admin/settings/mcp", new { switchOn = true });
        Assert.Equal(HttpStatusCode.OK, (await h.RpcAsync(t.Access, "tools/list")).Http.StatusCode);

        using var off = new TestApp(mongo);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await off.CreateClient().PostAsync("/mcp", null)).StatusCode);
    }

    [Fact]
    public async Task Public_settings_give_the_mcp_address_only_while_it_is_available()
    {
        async Task<System.Text.Json.JsonElement> McpOf(HttpClient c) =>
            (await c.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/settings/public")).GetProperty("mcp");

        var on = await McpOf(_app.CreateClient());
        Assert.True(on.GetProperty("available").GetBoolean());
        Assert.EndsWith("/mcp", on.GetProperty("url").GetString());

        var admin = await _app.AdminAsync();
        await admin.PutAsJsonAsync("/api/admin/settings/mcp", new { switchOn = false });
        var switchedOff = await McpOf(_app.CreateClient());
        Assert.False(switchedOff.GetProperty("available").GetBoolean());
        Assert.Equal("", switchedOff.GetProperty("url").GetString());

        using var off = new TestApp(mongo);
        Assert.False((await McpOf(off.CreateClient())).GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task Dynamic_registration_is_restricted()
    {
        var h = H();
        async Task<HttpStatusCode> Reg(object body) => (await h.Raw.PostAsJsonAsync("/oauth/register", body)).StatusCode;
        Assert.Equal(HttpStatusCode.BadRequest, await Reg(new { client_name = "x", redirect_uris = new[] { "http://evil.example/cb" } }));
        Assert.Equal(HttpStatusCode.BadRequest, await Reg(new { client_name = "x", redirect_uris = new[] { "https://a.example/cb#frag" } }));
        Assert.Equal(HttpStatusCode.BadRequest, await Reg(new { client_name = "x",
            redirect_uris = Enumerable.Range(1, 6).Select(i => $"https://a.example/{i}").ToArray() }));
        Assert.Equal(HttpStatusCode.BadRequest, await Reg(new { client_name = "x", redirect_uris = new[] { "https://a.example/cb" },
            token_endpoint_auth_method = "private_key_jwt" }));
        Assert.Equal(HttpStatusCode.BadRequest, await Reg(new { client_name = "x", redirect_uris = new[] { "https://a.example/cb" },
            grant_types = new[] { "password" } }));
        Assert.Equal(HttpStatusCode.Created, await Reg(new { client_name = "x", redirect_uris = new[] { "https://a.example/cb" } }));
    }

    [Fact]
    public async Task A_server_side_assistant_registers_with_a_secret_and_completes_the_flow()
    {
        // The registration ChatGPT sends: a confidential client with client_secret_post, its published redirect address.
        var h = H();
        const string redirect = "https://chatgpt.com/connector_platform_oauth_redirect";
        var reg = await h.Raw.PostAsJsonAsync("/oauth/register", new
        {
            client_name = "ChatGPT", redirect_uris = new[] { redirect }, grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" }, token_endpoint_auth_method = "client_secret_post",
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var body = await reg.Json();
        Assert.Equal("client_secret_post", body.GetProperty("token_endpoint_auth_method").GetString());
        var clientId = body.GetProperty("client_id").GetString()!;
        var secret = body.GetProperty("client_secret").GetString()!;
        Assert.Equal(0, body.GetProperty("client_secret_expires_at").GetInt64());

        var (verifier, challenge) = McpHarness.Pkce();
        var start = await h.Raw.GetAsync(McpHarness.AuthorizeUrl(clientId, challenge, McpHarness.UserScopes, redirect: redirect));
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var requestId = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(start.Headers.Location!.Query)["request"].ToString();
        var site = await _app.LoginAsync("dana@example.com");
        var decision = await McpHarness.DecideAsync(site, requestId, true, McpHarness.UserScopes);
        var back = await h.Raw.GetAsync(new Uri((await decision.Json()).GetProperty("redirectUrl").GetString()!).PathAndQuery);
        Assert.StartsWith(redirect, back.Headers.Location!.ToString());
        var code = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(back.Headers.Location!.Query)["code"].ToString();

        Task<HttpResponseMessage> Token(string? withSecret) => h.Raw.PostAsync("/oauth/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirect, ["client_id"] = clientId,
                ["code_verifier"] = verifier, ["resource"] = McpHarness.Resource,
            }.Concat(withSecret is null ? [] : [new KeyValuePair<string, string>("client_secret", withSecret)])));
        // Without the secret, or with a wrong one, the code is refused; with it, tokens are issued and /mcp answers.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Token(null)).StatusCode); // invalid_client
        Assert.NotEqual(HttpStatusCode.OK, (await Token("wrong")).StatusCode);
        var token = await Token(secret);
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        var access = (await token.Json()).GetProperty("access_token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await h.RpcAsync(access, "tools/list")).Http.StatusCode);
    }

    [Fact]
    public async Task Protected_resource_metadata_is_also_at_the_path_suffixed_address()
    {
        var doc = await H().Raw.GetFromJsonAsync<System.Text.Json.JsonElement>("/.well-known/oauth-protected-resource/mcp");
        Assert.Equal(McpHarness.Resource, doc.GetProperty("resource").GetString());
        Assert.Equal(7, doc.GetProperty("scopes_supported").GetArrayLength());
    }

    [Fact]
    public async Task Known_assistants_are_shown_as_verified()
    {
        var h = H();
        var user = await _app.LoginAsync("dana@example.com");
        var clientId = await h.RegisterAsync("Totally Claude", "https://claude.ai/api/mcp/auth_callback");
        var (_, challenge) = McpHarness.Pkce();
        var res = await h.Raw.GetAsync(McpHarness.AuthorizeUrl(clientId, challenge, ["offices:read"],
            redirect: "https://claude.ai/api/mcp/auth_callback"));
        var id = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(res.Headers.Location!.Query)["request"].ToString();
        var info = await (await user.GetAsync($"/oauth/authorize/request/{id}")).Json();
        Assert.True(info.GetProperty("verified").GetBoolean());
        Assert.Equal("Claude", info.GetProperty("clientName").GetString());
    }

    [Fact]
    public async Task Browser_origins_must_be_allowed()
    {
        var h = H();
        var t = await h.ConnectAsync(await _app.LoginAsync("dana@example.com"), McpHarness.UserScopes);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.RpcAsync(t.Access, "tools/list", origin: "https://evil.example")).Http.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await h.RpcAsync(t.Access, "tools/list", origin: "https://claude.ai")).Http.StatusCode);
    }

    [Fact]
    public async Task Denying_sends_access_denied_to_the_client()
    {
        var h = H();
        var user = await _app.LoginAsync("dana@example.com");
        var clientId = await h.RegisterAsync();
        var (_, challenge) = McpHarness.Pkce();
        var id = await h.StartAsync(clientId, challenge, McpHarness.UserScopes);
        var url = (await (await McpHarness.DecideAsync(user, id, false, [])).Json()).GetProperty("redirectUrl").GetString()!;
        Assert.StartsWith(McpHarness.Redirect + "?error=access_denied", url);
        Assert.Contains("state=st-123", url);
        Assert.Equal(HttpStatusCode.NotFound, (await McpHarness.DecideAsync(user, id, true, McpHarness.UserScopes)).StatusCode);
    }

    [Fact]
    public async Task Fresh_sign_in_means_this_session_not_another_sign_in_of_the_account()
    {
        var h = H();
        await _app.AdminAsync();
        // An older session of the admin (say, on a shared computer), with its own refresh token.
        var oldLogin = await (await _app.CreateClient().PostAsJsonAsync("/api/auth/google",
            new { credential = $"good:{TestApp.AdminEmail}:Owner Admin" })).Json();
        _app.Clock.Now = _app.Clock.Now.AddMinutes(11);
        var fresh = await _app.LoginAsync(TestApp.AdminEmail, "Owner Admin"); // The owner signs in somewhere else.

        var clientId = await h.RegisterAsync();
        var (_, challenge) = McpHarness.Pkce();
        var id = await h.StartAsync(clientId, challenge, McpHarness.AllScopes);
        var old = _app.CreateClient();
        old.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldLogin.GetProperty("accessToken").GetString());
        Assert.Equal("reauth_required", await (await McpHarness.DecideAsync(old, id, true, ["admin:write"])).Code());

        // Refreshing the old session keeps its original sign-in time.
        var refreshed = await (await _app.CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = oldLogin.GetProperty("refreshToken").GetString() })).Json();
        old.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.GetProperty("accessToken").GetString());
        Assert.Equal("reauth_required", await (await McpHarness.DecideAsync(old, id, true, ["admin:write"])).Code());

        Assert.False((await (await fresh.GetAsync($"/oauth/authorize/request/{id}")).Json()).GetProperty("needsFreshSignIn").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await McpHarness.DecideAsync(fresh, id, true, ["admin:write"])).StatusCode);
    }

    [Fact]
    public async Task Sensitive_scopes_end_seven_days_after_consent_however_often_refreshed()
    {
        // MongoDB's TTL monitor uses the real clock: keep the fake clock near it.
        _app.Clock.Now = DateTimeOffset.UtcNow;
        var h = H();
        var t = await h.ConnectAsync(await _app.AdminAsync(), McpHarness.AllScopes);
        _app.Clock.Now = _app.Clock.Now.AddDays(6);
        var day6 = await (await h.RefreshAsync(t.ClientId, t.Refresh)).Json();
        Assert.Contains("admin:write", day6.GetProperty("scope").GetString());
        _app.Clock.Now = _app.Clock.Now.AddDays(2);
        var day8 = await h.RefreshAsync(t.ClientId, day6.GetProperty("refresh_token").GetString()!);
        Assert.Equal(HttpStatusCode.BadRequest, day8.StatusCode);
    }

    [Fact]
    public async Task Authorize_without_state_is_refused()
    {
        var h = H();
        var clientId = await h.RegisterAsync();
        var (_, challenge) = McpHarness.Pkce();
        var url = McpHarness.AuthorizeUrl(clientId, challenge, McpHarness.UserScopes).Replace("&state=st-123", "");
        var res = await h.Raw.GetAsync(url);
        Assert.DoesNotContain("/McpConsent", res.Headers.Location?.ToString() ?? "");
        Assert.Contains("invalid_request", (res.Headers.Location?.ToString() ?? "") + await res.Content.ReadAsStringAsync());
    }
}
