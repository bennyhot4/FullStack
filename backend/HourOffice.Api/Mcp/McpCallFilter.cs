using System.Diagnostics;
using System.Text.Json;
using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using MongoDB.Driver;

namespace HourOffice.Api.Mcp;

/// <summary>Wraps every tools/call: strict arguments (S6), a 30-second limit and one audit row (S8).</summary>
public static class McpCallFilter
{
    public const int MaxArgumentBytes = 16 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Wrap(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) => async (rc, ct) =>
    {
        var http = rc.Services!.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        var tool = rc.Params?.Name ?? "";
        http.Items[McpCaller.ToolKey] = McpText.Clean(tool, 60);
        var sw = Stopwatch.StartNew();

        var invalid = CheckArguments(rc);
        if (invalid is not null)
        {
            http.Items[McpCaller.ErrorKey] = "invalid_arguments";
            await WriteAuditAsync(rc, http, tool, "error", sw);
            return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "invalid_arguments: " + invalid }] };
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        CallToolResult result;
        try
        {
            result = await next(rc, cts.Token);
        }
        catch (InputRequiredException)
        {
            // The assistant asks the person and calls again; that call reserves and is audited.
            if (http.Items[McpCaller.ReservationKey] is string reserved)
                await rc.Services!.GetRequiredService<MongoContext>().McpAudit
                    .DeleteOneAsync(a => a.Id == reserved, CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            http.Items[McpCaller.ErrorKey] ??= "error";
            await WriteAuditAsync(rc, http, tool, "error", sw);
            throw;
        }

        var code = http.Items[McpCaller.ErrorKey] as string;
        var outcome = result.IsError == true ? code == "declined" ? "declined" : "error" : "ok";
        if (result.IsError == true && code is null) http.Items[McpCaller.ErrorKey] = "error";
        await WriteAuditAsync(rc, http, tool, outcome, sw);
        return result;
    };

    /// <summary>Unknown argument names and oversized arguments are refused before the tool runs.</summary>
    private static string? CheckArguments(RequestContext<CallToolRequestParams> rc)
    {
        var args = rc.Params?.Arguments;
        if (args is null || args.Count == 0) return null;
        var size = args.Sum(a => a.Key.Length + a.Value.GetRawText().Length);
        if (size > MaxArgumentBytes && rc.Params!.Name != "admin_upload_image")
            return $"arguments are at most {MaxArgumentBytes / 1024} KB";
        if (rc.MatchedPrimitive is McpServerTool t
            && t.ProtocolTool.InputSchema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            var known = props.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var unknown = args.Keys.FirstOrDefault(k => !known.Contains(k));
            if (unknown is not null) return $"unknown argument '{McpText.Clean(unknown, 40)}'";
        }
        return null;
    }

    private static async Task WriteAuditAsync(RequestContext<CallToolRequestParams> rc, HttpContext http, string tool,
        string outcome, Stopwatch sw)
    {
        var services = rc.Services!;
        var db = services.GetRequiredService<MongoContext>();
        var clock = services.GetRequiredService<TimeProvider>();
        var caller = services.GetRequiredService<McpCaller>();
        var user = await http.CurrentUserAsync(db);
        var now = clock.GetUtcNow().UtcDateTime;
        var isWrite = http.Items[McpCaller.WriteKey] is true;
        var entry = new McpAuditEntry
        {
            At = now,
            UserId = user?.Id,
            Role = user?.Role ?? "",
            ClientId = McpText.Clean(caller.ClientId, 100),
            Tool = McpText.Clean(tool, 60),
            Outcome = outcome,
            ErrorCode = http.Items[McpCaller.ErrorKey] as string,
            TargetId = http.Items[McpCaller.TargetKey] as string,
            IsWrite = isWrite,
            IpHash = McpCaller.HashIp(http.Connection.RemoteIpAddress?.ToString(),
                services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value.JwtSigningKey),
            DurationMs = sw.ElapsedMilliseconds,
            ExpiresAt = now.AddDays(isWrite && tool.StartsWith("admin_", StringComparison.Ordinal) ? 365 : 90),
        };
        if (http.Items[McpCaller.ReservationKey] is string reserved)
        {
            // The per-hour limit's reservation becomes this call's row (a failed call then no longer counts).
            entry.Id = reserved;
            await db.McpAudit.ReplaceOneAsync(a => a.Id == reserved, entry, new ReplaceOptions { IsUpsert = true },
                CancellationToken.None);
        }
        else await db.McpAudit.InsertOneAsync(entry, cancellationToken: CancellationToken.None);
        if (isWrite && tool.StartsWith("admin_", StringComparison.Ordinal))
            services.GetRequiredService<ILoggerFactory>().CreateLogger("HourOffice.Mcp")
                .LogInformation("MCP admin write {Tool} by {UserId} via {ClientId}: {Outcome}", entry.Tool, entry.UserId,
                    entry.ClientId, outcome);
    }
}
