using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace HourOffice.Tests;

/// <summary>Drives the OAuth flow and MCP JSON-RPC calls the way an AI assistant does (plan 011).</summary>
public sealed class McpHarness(TestApp app)
{
    public const string Redirect = "http://127.0.0.1:7777/callback";
    public const string Resource = "http://localhost/mcp";
    public static readonly string[] UserScopes = ["offices:read", "bookings:read", "bookings:write", "clients:basic"];
    public static readonly string[] AllScopes =
        ["offices:read", "bookings:read", "bookings:write", "clients:basic", "clients:full", "admin:read", "admin:write"];

    public HttpClient Raw { get; } = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task<string> RegisterAsync(string name = "Test Assistant", params string[] redirectUris)
    {
        var res = await Raw.PostAsJsonAsync("/oauth/register", new
        {
            client_name = name,
            redirect_uris = redirectUris.Length > 0 ? redirectUris : [Redirect],
            token_endpoint_auth_method = "none",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Json()).GetProperty("client_id").GetString()!;
    }

    public static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    public static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string AuthorizeUrl(string clientId, string challenge, IEnumerable<string> scopes, string method = "S256",
        string redirect = Redirect) =>
        QueryHelpers.AddQueryString("/oauth/authorize", new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirect,
            ["scope"] = string.Join(' ', scopes),
            ["state"] = "st-123",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = method,
            ["resource"] = Resource,
        });

    /// <summary>First leg: returns the consent request id from the redirect to the site.</summary>
    public async Task<string> StartAsync(string clientId, string challenge, IEnumerable<string> scopes)
    {
        var res = await Raw.GetAsync(AuthorizeUrl(clientId, challenge, scopes));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        var location = res.Headers.Location!.ToString();
        Assert.StartsWith("http://site.test/McpConsent?request=", location);
        return QueryHelpers.ParseQuery(new Uri(location).Query)["request"].ToString();
    }

    public static Task<HttpResponseMessage> DecideAsync(HttpClient site, string requestId, bool approve, IEnumerable<string> scopes) =>
        site.PostAsJsonAsync("/oauth/authorize/decision", new { requestId, approve, scopes });

    /// <summary>Second leg: follows the approval back to /oauth/authorize and returns the authorization code.</summary>
    public async Task<string> CodeAsync(string redirectUrl)
    {
        var u = new Uri(redirectUrl);
        var res = await Raw.GetAsync(u.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        var location = res.Headers.Location!;
        Assert.StartsWith(Redirect, location.ToString());
        var q = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("st-123", q["state"].ToString());
        return q["code"].ToString();
    }

    public Task<HttpResponseMessage> TokenAsync(string clientId, string code, string verifier) =>
        Raw.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = Redirect,
            ["client_id"] = clientId, ["code_verifier"] = verifier, ["resource"] = Resource,
        }));

    public Task<HttpResponseMessage> RefreshAsync(string clientId, string refreshToken) =>
        Raw.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken, ["client_id"] = clientId,
            ["resource"] = Resource,
        }));

    public sealed record Tokens(string ClientId, string Access, string Refresh);

    /// <summary>The whole flow for a signed-in site user: register, authorize, approve <paramref name="grant"/>, token.</summary>
    public async Task<Tokens> ConnectAsync(HttpClient site, IEnumerable<string>? request = null, IEnumerable<string>? grant = null)
    {
        var clientId = await RegisterAsync();
        var (verifier, challenge) = Pkce();
        var requested = (request ?? AllScopes).ToList();
        var id = await StartAsync(clientId, challenge, requested);
        var decision = await DecideAsync(site, id, true, grant ?? requested);
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        var code = await CodeAsync((await decision.Json()).GetProperty("redirectUrl").GetString()!);
        var token = await TokenAsync(clientId, code, verifier);
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        var body = await token.Json();
        return new Tokens(clientId, body.GetProperty("access_token").GetString()!, body.GetProperty("refresh_token").GetString()!);
    }

    private int _id;

    /// <summary>One JSON-RPC request to /mcp. Returns the HTTP response and the parsed JSON-RPC message (or null).</summary>
    public async Task<(HttpResponseMessage Http, JsonElement? Message)> RpcAsync(string? access, string method, object? @params = null,
        string? origin = null)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = ++_id, method, @params = @params ?? new { } }),
        };
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        msg.Headers.Add("MCP-Protocol-Version", "2025-06-18");
        if (access is not null) msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        if (origin is not null) msg.Headers.Add("Origin", origin);
        var res = await Raw.SendAsync(msg);
        if (!res.IsSuccessStatusCode) return (res, null);
        var text = await res.Content.ReadAsStringAsync();
        var json = text.TrimStart().StartsWith('{') ? text
            : string.Join("", text.Split('\n').Where(l => l.StartsWith("data:")).Select(l => l[5..].Trim()).Take(1));
        return (res, JsonDocument.Parse(json).RootElement.Clone());
    }

    public async Task<List<string>> ToolNamesAsync(string access)
    {
        var (_, m) = await RpcAsync(access, "tools/list");
        return m!.Value.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!).ToList();
    }

    /// <summary>tools/call. Returns the result object (with isError / structuredContent / content).</summary>
    public async Task<JsonElement> CallAsync(string access, string tool, object? args = null)
    {
        var (http, m) = await RpcAsync(access, "tools/call", new { name = tool, arguments = args ?? new { } });
        Assert.True(http.IsSuccessStatusCode, $"HTTP {(int)http.StatusCode}");
        var msg = m!.Value;
        if (msg.TryGetProperty("error", out var error))
            return JsonDocument.Parse(JsonSerializer.Serialize(new { isError = true, content = new[] { new { type = "text", text = error.GetProperty("message").GetString() } } })).RootElement;
        return msg.GetProperty("result");
    }

    public static bool IsError(JsonElement result) => result.TryGetProperty("isError", out var e) && e.GetBoolean();

    /// <summary>The tool error text without the SDK's "An error occurred invoking 'tool': " prefix, so it starts with our code.</summary>
    public static string ErrorText(JsonElement result)
    {
        var text = ErrorTextRaw(result);
        var i = text.IndexOf("': ", StringComparison.Ordinal);
        return text.StartsWith("An error occurred invoking", StringComparison.Ordinal) && i > 0 ? text[(i + 3)..] : text;
    }

    public static string ErrorTextRaw(JsonElement result) =>
        result.GetProperty("content").EnumerateArray().First().GetProperty("text").GetString() ?? "";

    public static JsonElement Data(JsonElement result)
    {
        Assert.False(IsError(result), IsError(result) ? ErrorText(result) : "");
        return result.GetProperty("structuredContent");
    }
}
