using System.Text.RegularExpressions;
using HourOffice.Api.Data;
using HourOffice.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

public static partial class SettingsEndpoints
{
    private static readonly Dictionary<string, string> AllowedImageTypes = new()
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/gif"] = ".gif",
    };

    public static void Map(WebApplication app)
    {
        // Public (no sign-in): the login page needs branding and the Google client id.
        app.MapGet("/api/settings/public", async (MongoContext db, IOptions<AuthOptions> auth, IOptions<CurrencyOptions> cur,
            BookingRules rules, CancellationToken ct) =>
        {
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
                banners = Banners(all).Select(b => new { id = b.Id, url = b.Value }),
                currency = new { @base = cur.Value.Base, displaySymbols = cur.Value.DisplaySymbols },
                booking = new { rules.Options.OpenHour, rules.Options.CloseHour, rules.Options.MaxDaysAhead, rules.Options.TimeZone },
                auth = new
                {
                    googleClientId = auth.Value.GoogleClientId,
                    devLoginEnabled = auth.Value.DevLoginEnabled && !app.Environment.IsProduction(),
                    allowOpenSignup = auth.Value.AllowOpenSignup,
                },
            });
        });

        var admin = app.MapGroup("/api/admin/settings").RequireAdmin();

        admin.MapPut("/branding", async (BrandingRequest req, MongoContext db, CancellationToken ct) =>
        {
            foreach (var c in new[] { req.ButtonsColor, req.BackgroundColor, req.PagesColor })
                if (!string.IsNullOrEmpty(c) && !HexColor().IsMatch(c)) return Api.BadRequest("invalid_color");
            var values = new Dictionary<string, string?>
            {
                [SettingKeys.BrandTitle] = req.Title?.Trim(),
                [SettingKeys.BrandDescription] = req.Description?.Trim(),
                [SettingKeys.BrandLogo] = req.Logo?.Trim(),
                [SettingKeys.BrandButtonsColor] = req.ButtonsColor,
                [SettingKeys.BrandBackgroundColor] = req.BackgroundColor,
                [SettingKeys.BrandPagesColor] = req.PagesColor,
            };
            foreach (var (k, v) in values) await UpsertAsync(db, k, v ?? "", ct);
            return Results.NoContent();
        });

        admin.MapPut("/home-banner-image", async (SettingValueRequest req, MongoContext db, CancellationToken ct) =>
        {
            await UpsertAsync(db, SettingKeys.HomeBannerImage, req.Value?.Trim() ?? "", ct);
            return Results.NoContent();
        });

        admin.MapPost("/banners", async (SettingValueRequest req, MongoContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Value)) return Api.BadRequest("url_required");
            var all = await db.Settings.Find(s => s.Key.StartsWith(SettingKeys.HomeBannerPrefix)).ToListAsync(ct);
            var next = Banners(all).Select(b => int.Parse(b.Key[SettingKeys.HomeBannerPrefix.Length..])).DefaultIfEmpty(0).Max() + 1;
            var s = new SiteSetting { Key = $"{SettingKeys.HomeBannerPrefix}{next}", Value = req.Value.Trim() };
            await db.Settings.InsertOneAsync(s, cancellationToken: ct);
            return Results.Ok(new { id = s.Id, url = s.Value });
        });

        admin.MapDelete("/banners/{id}", async (string id, MongoContext db, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound();
            var res = await db.Settings.DeleteOneAsync(s => s.Id == id && s.Key.StartsWith(SettingKeys.HomeBannerPrefix), ct);
            return res.DeletedCount == 0 ? Api.NotFound() : Results.NoContent();
        });

        app.MapGroup("/api/admin/upload").RequireAdmin().MapPost("", async (IFormFile? file, HttpRequest request,
            IFileStorage storage, IOptions<StorageOptions> opts, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0) return Api.BadRequest("file_required");
            if (file.Length > opts.Value.MaxBytes) return Api.BadRequest("file_too_large");
            if (!AllowedImageTypes.TryGetValue(file.ContentType, out var ext)) return Api.BadRequest("unsupported_type");
            await using var stream = file.OpenReadStream();
            var url = await storage.SaveAsync(stream, ext, file.ContentType, request, ct);
            return Results.Ok(new { fileUrl = url });
        }).DisableAntiforgery();
    }

    /// <summary>Banner keys are home_banner_1..N (home_banner_image is a different setting).</summary>
    private static IEnumerable<SiteSetting> Banners(IEnumerable<SiteSetting> all) =>
        all.Where(s => s.Key.StartsWith(SettingKeys.HomeBannerPrefix) && int.TryParse(s.Key[SettingKeys.HomeBannerPrefix.Length..], out _))
            .OrderBy(s => int.Parse(s.Key[SettingKeys.HomeBannerPrefix.Length..]));

    private static Task UpsertAsync(MongoContext db, string key, string value, CancellationToken ct) =>
        db.Settings.UpdateOneAsync(s => s.Key == key, Builders<SiteSetting>.Update.Set(s => s.Value, value)
            .SetOnInsert(s => s.Id, MongoDB.Bson.ObjectId.GenerateNewId().ToString()),
            new UpdateOptions { IsUpsert = true }, ct);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
