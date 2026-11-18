using System.Threading.RateLimiting;
using HourOffice.Api;
using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using HourOffice.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<MongoOptions>(config.GetSection("Mongo"));
builder.Services.Configure<AuthOptions>(config.GetSection("Auth"));
builder.Services.Configure<CorsOptions>(config.GetSection("Cors"));
builder.Services.Configure<BookingOptions>(config.GetSection("Booking"));
builder.Services.Configure<CurrencyOptions>(config.GetSection("Currency"));
builder.Services.Configure<StorageOptions>(config.GetSection("Storage"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<BookingRules>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<AdminClientSync>();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddSingleton<IFileStorage>(sp =>
    sp.GetRequiredService<IOptions<StorageOptions>>().Value.Provider.Equals("S3", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<S3FileStorage>(sp)
        : ActivatorUtilities.CreateInstance<LocalFileStorage>(sp));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthOptions>, TimeProvider>((jwt, auth, clock) =>
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = auth.Value.JwtIssuer,
            ValidAudience = auth.Value.JwtIssuer,
            IssuerSigningKey = TokenService.SigningKey(auth.Value),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "email",
            RoleClaimType = "role",
            // Use the app clock (testable) instead of DateTime.UtcNow.
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                return (notBefore is null || notBefore <= now.AddSeconds(30)) && expires is not null && expires > now.AddSeconds(-30);
            },
        };
    });
builder.Services.AddAuthorization();

// The frontend is deployed separately, so its origin(s) must be allowed explicitly.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Location");
}));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddProblemDetails();

// Hosting platforms (Cloud Run, Render...) sit behind a proxy: take the client IP from X-Forwarded-For.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Local uploads (development). In production use Storage:Provider=S3.
if (app.Services.GetRequiredService<IFileStorage>() is LocalFileStorage local)
{
    Directory.CreateDirectory(local.Root);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(local.Root), RequestPath = "/uploads" });
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
AuthEndpoints.Map(app);
TermsEndpoints.Map(app);
OfficeEndpoints.Map(app);
BookingEndpoints.Map(app);
ClientEndpoints.Map(app);
DistributionListEndpoints.Map(app);
AdminEndpoints.Map(app);
SettingsEndpoints.Map(app);

// Create indexes, seed the initial terms of use, and fill the admins' client lists.
var db = app.Services.GetRequiredService<MongoContext>();
await db.EnsureIndexesAsync();
await TermsSeeder.SeedAsync(db, app.Environment, app.Services.GetRequiredService<TimeProvider>());
// Once per database: every active admin gets a copy of every registered user in their client list.
await app.Services.GetRequiredService<AdminClientSync>().FillAllAdminsOnceAsync();

app.Run();

public partial class Program;
