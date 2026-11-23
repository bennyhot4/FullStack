using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using HourOffice.Api;
using HourOffice.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HourOffice.Tests;

/// <summary>One real mongod process shared by all tests. The binary ships in the EphemeralMongo7 runtime
/// NuGet package (no network needed); we start it directly because EphemeralMongo's own shutdown code
/// targets the 2.x driver.</summary>
public sealed class MongoFixture : IDisposable
{
    private readonly Process _process;
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "ho-mongo-" + Guid.NewGuid().ToString("N"));
    public string ConnectionString { get; }

    public MongoFixture()
    {
        var rid = OperatingSystem.IsWindows() ? "win-x64" : OperatingSystem.IsMacOS() ? "osx-arm64" : "linux-x64";
        var exe = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", "mongodb", "bin",
            OperatingSystem.IsWindows() ? "mongod.exe" : "mongod");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(exe, File.GetUnixFileMode(exe) | UnixFileMode.UserExecute);
        Directory.CreateDirectory(_dataDir);
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        _process = Process.Start(new ProcessStartInfo(exe,
            // --nounixsocket does not exist on Windows; mongod there exits with "unrecognised option".
            $"--dbpath \"{_dataDir}\" --port {port} --bind_ip 127.0.0.1 --quiet" + (OperatingSystem.IsWindows() ? "" : " --nounixsocket"))
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        })!;
        _process.OutputDataReceived += (_, _) => { };
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        ConnectionString = $"mongodb://127.0.0.1:{port}/?directConnection=true";
        var client = new MongoClient(ConnectionString);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try { client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1)); break; }
            catch when (DateTime.UtcNow < deadline && !_process.HasExited) { Thread.Sleep(200); }
        }
    }

    public void Dispose()
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); _process.WaitForExit(5000); } catch { }
        try { Directory.Delete(_dataDir, recursive: true); } catch { }
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<MongoFixture>;

public sealed class FakeClock : TimeProvider
{
    // Monday 2026-11-09 08:00 in Israel (06:00 UTC).
    public DateTimeOffset Now { get; set; } = new(2026, 11, 9, 6, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Accepts tokens shaped "good:email[:name]" so tests never call Google.</summary>
public sealed class FakeGoogle : IGoogleTokenValidator
{
    public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var parts = idToken.Split(':');
        if (parts.Length < 2 || parts[0] != "good") return Task.FromResult<GoogleIdentity?>(null);
        var name = parts.ElementAtOrDefault(2) ?? "Test User";
        var names = name.Split(' ', 2);
        return Task.FromResult<GoogleIdentity?>(new GoogleIdentity("sub-" + parts[1], parts[1], true, name,
            names[0], names.ElementAtOrDefault(1), null));
    }
}

public sealed class TestApp : WebApplicationFactory<Program>
{
    public const string AdminEmail = "owner@example.com";
    public FakeClock Clock { get; } = new();
    private readonly string _conn;
    private readonly string _dbName = "t" + Guid.NewGuid().ToString("N");

    private readonly Dictionary<string, string> _settings;

    public TestApp(MongoFixture mongo, Dictionary<string, string>? settings = null)
    {
        _conn = mongo.ConnectionString;
        _settings = settings ?? new();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Mongo:ConnectionString", _conn);
        builder.UseSetting("Mongo:Database", _dbName);
        builder.UseSetting("Auth:JwtSigningKey", "test-signing-key-0123456789abcdef0123456789");
        builder.UseSetting("Auth:DevLoginEnabled", "true");
        builder.UseSetting("Auth:GoogleClientId", "test-client");
        builder.UseSetting("Auth:AdminEmails:0", AdminEmail);
        builder.UseSetting("Storage:LocalPath", Path.Combine(Path.GetTempPath(), _dbName));
        // MCP (plan 011): keys made once per test run (generating RSA keys per app is slow), like the secret store.
        builder.UseSetting("Mcp:SigningKey", TestKeys.SigningPem);
        builder.UseSetting("Mcp:EncryptionKey", TestKeys.EncryptionKey);
        builder.UseSetting("Mcp:Issuer", "http://localhost/");
        builder.UseSetting("Mcp:SiteUrl", "http://site.test/");
        // Field-level encryption on in every test (plan 011, E3), with one key vault for the whole run.
        builder.UseSetting("FieldEncryption:Mode", "encrypt");
        builder.UseSetting("FieldEncryption:KmsProvider", "local");
        builder.UseSetting("FieldEncryption:LocalMasterKey", TestKeys.LocalMasterKey);
        builder.UseSetting("FieldEncryption:KeyVaultDatabase", "keyvault_tests");
        foreach (var (k, v) in _settings) builder.UseSetting(k, v);
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
            s.RemoveAll<IGoogleTokenValidator>();
            s.AddSingleton<IGoogleTokenValidator, FakeGoogle>();
        });
    }

    public async Task<HttpClient> LoginAsync(string email, string name = "Test User", bool ready = true)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/google", new { credential = $"good:{email}:{name}" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        if (ready)
        {
            (await client.PutAsJsonAsync("/api/me", new { firstName = name.Split(' ')[0], lastName = "Tester", phone = "050-1234567" }))
                .EnsureSuccessStatusCode();
            var terms = await client.GetFromJsonAsync<JsonElement>("/api/terms/current");
            (await client.PostAsJsonAsync("/api/me/accept-terms", new { version = terms.GetProperty("version").GetInt32() }))
                .EnsureSuccessStatusCode();
        }
        return client;
    }

    public Task<HttpClient> AdminAsync() => LoginAsync(AdminEmail, "Owner Admin");

    public static async Task<string> CreateOfficeAsync(HttpClient admin, string name = "Room A", decimal rate = 50, int capacity = 4, bool available = true)
    {
        var res = await admin.PostAsJsonAsync("/api/admin/offices", new
        {
            name, description = "Quiet room", imageUrl = "", capacity, hourlyRate = rate,
            amenities = new[] { "WiFi", "Coffee" }, location = "Tel Aviv", isAvailable = available,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
}

public static class Http
{
    public static async Task<JsonElement> Json(this HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string?> Code(this HttpResponseMessage res) =>
        (await res.Json()).TryGetProperty("code", out var c) ? c.GetString() : null;
}

/// <summary>MCP token keys for the whole test run.</summary>
public static class TestKeys
{
    public static readonly string SigningPem = System.Security.Cryptography.RSA.Create(2048).ExportPkcs8PrivateKeyPem();
    public static readonly string EncryptionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    public static readonly string LocalMasterKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(96));
}
