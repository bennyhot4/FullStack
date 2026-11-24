using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace HourOffice.Api.Mcp;

/// <summary>Plan 011, S8: a burst of failed authentications (401) from one IP on the MCP and OAuth paths is logged as a
/// warning, once per IP per minute, so Cloud Logging can alert. Counted per instance (Cloud Run runs at most two).
/// The IP is logged only as its keyed hash, like in the audit log.</summary>
public sealed class McpAuthFailureMonitor(TimeProvider clock, IOptions<AuthOptions> auth, ILogger<McpAuthFailureMonitor> log)
{
    public const int Threshold = 20;
    private const int MaxTrackedIps = 10_000;
    private readonly ConcurrentDictionary<string, Window> _windows = new();

    private sealed class Window(long minute)
    {
        public long Minute { get; } = minute;
        public int Count;
    }

    /// <summary>Counts one 401 for the request's IP; returns true when this one crossed the threshold (and was logged).</summary>
    public bool Record(HttpContext ctx)
    {
        var minute = clock.GetUtcNow().ToUnixTimeSeconds() / 60;
        // Memory bound: many distinct IPs (e.g. a distributed scan) only reset the counters early.
        if (_windows.Count > MaxTrackedIps) _windows.Clear();
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var window = _windows.AddOrUpdate(ip, _ => new Window(minute), (_, w) => w.Minute == minute ? w : new Window(minute));
        if (Interlocked.Increment(ref window.Count) != Threshold) return false;
        log.LogWarning("MCP auth failure burst: {Count} 401 responses in a minute from IP {IpHash} on {Path}",
            Threshold, McpCaller.HashIp(ip, auth.Value.JwtSigningKey), ctx.Request.Path.Value);
        return true;
    }
}
