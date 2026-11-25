using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace HourOffice.Tests;

/// <summary>Every MCP protocol revision the SDK supports, with and without elicitation, can do the whole booking
/// cycle (list, book, see it, cancel). Assistants differ in which revision they speak and whether they can show a
/// question, and a gap here means "connected, but cannot book" for that assistant (ChatGPT, 2026-11-25).</summary>
[Collection("api")]
public sealed class McpProtocolTests(MongoFixture mongo) : IDisposable
{
    private readonly TestApp _app = new(mongo, new() { ["Mcp:Enabled"] = "true" });
    public void Dispose() => _app.Dispose();

    private const string July2026 = "2026-07-28";

    public static TheoryData<string, bool> Clients()
    {
        var data = new TheoryData<string, bool>();
        foreach (var v in new[] { "2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25", July2026 })
        {
            data.Add(v, false);
            data.Add(v, true);
        }
        return data;
    }

    /// <summary>A client of one protocol revision: the handshake (up to 2025-11-25) or per-request metadata (2026-07-28),
    /// and the headers each revision sends.</summary>
    private sealed class Client(McpHarness h, string access, string version, bool elicitation)
    {
        private string? _session;
        private bool PerRequest => string.CompareOrdinal(version, July2026) >= 0;
        private object Capabilities => elicitation ? new { elicitation = new { } } : new { };

        public async Task StartAsync()
        {
            if (PerRequest) return;
            var (http, m) = await h.RpcAsync(access, "initialize", new
            {
                protocolVersion = version, capabilities = Capabilities,
                clientInfo = new { name = "matrix-" + version, version = "1.0.0" },
            }, protocolVersion: version);
            Assert.True(http.IsSuccessStatusCode, $"initialize: HTTP {(int)http.StatusCode}");
            Assert.Equal(version, m!.Value.GetProperty("result").GetProperty("protocolVersion").GetString());
            _session = http.Headers.TryGetValues("Mcp-Session-Id", out var ids) ? ids.First() : null;
            await h.RpcAsync(access, "notifications/initialized", null, protocolVersion: version, sessionId: _session);
        }

        public async Task<JsonElement> RpcAsync(string method, Dictionary<string, object>? p = null)
        {
            p ??= [];
            if (PerRequest)
                p["_meta"] = new Dictionary<string, object>
                {
                    [MetaKeys.ProtocolVersion] = version,
                    [MetaKeys.ClientInfo] = new { name = "matrix-" + version, version = "1.0.0" },
                    [MetaKeys.ClientCapabilities] = Capabilities,
                };
            var (http, m) = await h.RpcAsync(access, method, p, protocolVersion: version, sessionId: _session);
            Assert.True(http.IsSuccessStatusCode, $"{method}: HTTP {(int)http.StatusCode}");
            Assert.False(m!.Value.TryGetProperty("error", out var e), $"{method}: {e}");
            return m.Value.GetProperty("result");
        }

        /// <summary>tools/call; when the server asks for confirmation (2026-07-28 with elicitation), says yes and calls
        /// again, as such an assistant does after the person confirms. Returns the structured result.</summary>
        public async Task<JsonElement> CallAsync(string tool, object args, bool expectQuestion = false)
        {
            var result = await RpcAsync("tools/call", new() { ["name"] = tool, ["arguments"] = args });
            if (expectQuestion)
            {
                Assert.True(result.TryGetProperty("inputRequests", out _), $"{tool} should ask first: {result}");
                result = await RpcAsync("tools/call", new()
                {
                    ["name"] = tool, ["arguments"] = args,
                    ["inputResponses"] = new { confirm = new { action = "accept", content = new { confirm = true } } },
                });
            }
            Assert.False(McpHarness.IsError(result), $"{tool}: {result}");
            return McpHarness.Data(result);
        }
    }

    [Theory]
    [MemberData(nameof(Clients))]
    public async Task Every_protocol_revision_can_list_book_and_cancel(string version, bool elicitation)
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin, rate: 50);
        var h = new McpHarness(_app);
        var site = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var t = await h.ConnectAsync(site, McpHarness.UserScopes);
        var c = new Client(h, t.Access, version, elicitation);
        var asks = elicitation && string.CompareOrdinal(version, July2026) >= 0;

        await c.StartAsync();
        var tools = (await c.RpcAsync("tools/list")).GetProperty("tools").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()).ToList();
        Assert.Contains("create_booking", tools);
        Assert.Contains("cancel_booking", tools);

        Assert.Contains(office, (await c.CallAsync("list_offices", new { })).ToString());
        await c.CallAsync("get_office_availability", new { officeId = office, date = "2026-11-10" });

        var booked = await c.CallAsync("create_booking",
            new { officeId = office, date = "2026-11-10", startTime = "16:00", endTime = "17:00" }, asks);
        Assert.Equal("confirmed", booked.GetProperty("status").GetString());
        Assert.Equal(50m, booked.GetProperty("totalPrice").GetDecimal());
        var id = booked.GetProperty("bookingId").GetString()!;

        var mine = await c.CallAsync("list_my_bookings", new { });
        Assert.Equal(id, mine.GetProperty("upcoming")[0].GetProperty("bookingId").GetString());

        var cancelled = await c.CallAsync("cancel_booking", new { bookingId = id }, asks);
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        var onSite = await (await site.GetAsync("/api/bookings/mine")).Json();
        Assert.Equal("cancelled", onSite.GetProperty("upcoming").EnumerateArray().Concat(onSite.GetProperty("past").EnumerateArray())
            .Single(b => b.GetProperty("id").GetString() == id).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_client_asking_for_an_unknown_revision_is_told_the_supported_ones()
    {
        var h = new McpHarness(_app);
        var site = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var t = await h.ConnectAsync(site, McpHarness.UserScopes);

        // Handshake clients: initialize with an unknown version → a JSON-RPC error that lists the handshake versions
        // (the SDK's behaviour, so the client can retry with one), never a crash.
        var (http, m) = await h.RpcAsync(t.Access, "initialize", new
        {
            protocolVersion = "2099-01-01", capabilities = new { }, clientInfo = new { name = "future", version = "9" },
        }, protocolVersion: null);
        Assert.True(http.IsSuccessStatusCode, $"HTTP {(int)http.StatusCode}");
        var initError = m!.Value.GetProperty("error");
        Assert.Equal(-32022, initError.GetProperty("code").GetInt32());
        Assert.Contains("2025-11-25", initError.GetProperty("data").GetProperty("supported").EnumerateArray()
            .Select(v => v.GetString()));

        // Per-request clients: an unknown version in the header → a clear 400 listing the supported versions (the
        // client retries with one of them), never a 5xx.
        var (bad, _) = await h.RpcAsync(t.Access, "tools/list", null, protocolVersion: "2099-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var error = (await bad.Json()).GetProperty("error");
        Assert.Equal(-32022, error.GetProperty("code").GetInt32());
        Assert.Contains(July2026, error.GetProperty("data").GetProperty("supported").EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public async Task A_client_that_sends_no_protocol_header_still_works()
    {
        // 2024-11-05 / 2025-03-26 clients do not send MCP-Protocol-Version after initialize.
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var h = new McpHarness(_app);
        var site = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var t = await h.ConnectAsync(site, McpHarness.UserScopes);

        var msg = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                jsonrpc = "2.0", id = 1, method = "tools/call",
                @params = new { name = "list_offices", arguments = new { } },
            }),
        };
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t.Access);
        var res = await h.Raw.SendAsync(msg);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains(office, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Opening_the_event_stream_is_refused_cleanly()
    {
        // Some clients try GET /mcp for server messages. The stateless server has none; the answer must be a clean
        // 4xx (clients then carry on with POST), never a 5xx.
        var h = new McpHarness(_app);
        var site = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var t = await h.ConnectAsync(site, McpHarness.UserScopes);

        var msg = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t.Access);
        var res = await h.Raw.SendAsync(msg);
        Assert.InRange((int)res.StatusCode, 400, 499);
    }
}
