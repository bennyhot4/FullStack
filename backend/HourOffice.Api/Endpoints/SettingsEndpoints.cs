using HourOffice.Api.Data;
using HourOffice.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

/// <summary>Settings routes. Branding, banners and uploads live in <see cref="SettingsService"/>, shared with the
/// MCP tools.</summary>
public static class SettingsEndpoints
{
    public static void Map(WebApplication app)
    {
        // Public (no sign-in): the login page needs branding and the Google client id.
        app.MapGet("/api/settings/public", async (MongoContext db, IOptions<AuthOptions> auth, IOptions<CurrencyOptions> cur,
            BookingRules rules, Mcp.McpGate mcpGate, IOptions<McpOptions> mcp, CancellationToken ct) =>
        {
            // Plan 012: the "Connect an AI assistant" page needs the MCP address, and hides itself while MCP is off.
            var mcpAvailable = await mcpGate.IsEnabledAsync(ct);
            var all = await db.Settings.Find(FilterDefinition<SiteSetting>.Empty).ToListAsync(ct);
            string Get(string key) => all.FirstOrDefault(s => s.Key == key)?.Value is { Length: > 0 } v ? v : SettingKeys.BrandDefaults[key];
            return Results.Ok(new
            {
                branding = new
                {
                    title = Get(SettingKeys.BrandTitle),
                    description = Get(SettingKeys.BrandDescription),
                    logo = Get(SettingKeys.BrandLogo),
                    buttonsColor = Get(SettingKeys.BrandButtonsColor),
                    backgroundColor = Get(SettingKeys.BrandBackgroundColor),
                    pagesColor = Get(SettingKeys.BrandPagesColor),
                },
                homeBannerImage = all.FirstOrDefault(s => s.Key == SettingKeys.HomeBannerImage)?.Value ?? "",
                banners = SettingsService.Banners(all).Select(b => new { id = b.Id, url = b.Value }),
                currency = new { @base = cur.Value.Base, displaySymbols = cur.Value.DisplaySymbols },
                booking = new { rules.Options.OpenHour, rules.Options.CloseHour, rules.Options.MaxDaysAhead, rules.Options.TimeZone },
                mcp = new { available = mcpAvailable, url = mcpAvailable ? mcp.Value.Resource : "" },
                auth = new
                {
                    googleClientId = auth.Value.GoogleClientId,
                    devLoginEnabled = auth.Value.DevLoginEnabled && !app.Environment.IsProduction(),
                    allowOpenSignup = auth.Value.AllowOpenSignup,
                },
            });
        });

        var admin = app.MapGroup("/api/admin/settings").RequireAdmin();

        admin.MapPut("/branding", async (BrandingRequest req, SettingsService svc, CancellationToken ct) =>
            (await svc.UpdateBrandingAsync(req, ct))?.ToResult() ?? Results.NoContent());

        admin.MapPut("/home-banner-image", async (SettingValueRequest req, SettingsService svc, CancellationToken ct) =>
        {
            await svc.SetHomeBannerImageAsync(req.Value, ct);
            return Results.NoContent();
        });

        admin.MapPost("/banners", async (SettingValueRequest req, SettingsService svc, CancellationToken ct) =>
            (await svc.AddBannerAsync(req.Value, ct)).ToResult());

        admin.MapDelete("/banners/{id}", async (string id, SettingsService svc, CancellationToken ct) =>
            (await svc.DeleteBannerAsync(id, ct))?.ToResult() ?? Results.NoContent());

        app.MapGroup("/api/admin/upload").RequireAdmin().MapPost("", async (IFormFile? file, HttpRequest request,
            SettingsService svc, IOptions<StorageOptions> opts, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0) return Api.BadRequest("file_required");
            if (file.Length > opts.Value.MaxBytes) return Api.BadRequest("file_too_large");
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            var r = await svc.SaveImageAsync(buffer.ToArray(), file.ContentType,
                SettingsService.ImageTypes.Keys.ToList(), request, ct);
            return r.Error?.ToResult() ?? Results.Ok(new { fileUrl = r.Value });
        }).DisableAntiforgery();
    }
}
