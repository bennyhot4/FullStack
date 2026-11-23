using System.Text.RegularExpressions;
using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

/// <summary>Branding, banners and image uploads, shared by <see cref="SettingsEndpoints"/> and the MCP tools.</summary>
public sealed partial class SettingsService(MongoContext db, IFileStorage storage, IOptions<StorageOptions> storageOptions)
{
    /// <summary>Content types the site accepts, with the file extension stored.</summary>
    public static readonly IReadOnlyDictionary<string, string> ImageTypes = new Dictionary<string, string>
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/gif"] = ".gif",
    };

    public async Task<Dictionary<string, string>> BrandingAsync(CancellationToken ct)
    {
        var all = await db.Settings.Find(s => SettingKeys.BrandDefaults.Keys.Contains(s.Key)).ToListAsync(ct);
        return SettingKeys.BrandDefaults.ToDictionary(d => d.Key,
            d => all.FirstOrDefault(s => s.Key == d.Key)?.Value is { Length: > 0 } v ? v : d.Value);
    }

    /// <summary>Saves every field: a null field is stored as empty (the site always sends all of them).</summary>
    public async Task<ServiceError?> UpdateBrandingAsync(BrandingRequest req, CancellationToken ct)
    {
        foreach (var c in new[] { req.ButtonsColor, req.BackgroundColor, req.PagesColor })
            if (!string.IsNullOrEmpty(c) && !HexColor().IsMatch(c)) return ServiceError.BadRequest("invalid_color");
        if ((req.Title?.Length ?? 0) > 200 || (req.Description?.Length ?? 0) > 2000 || (req.Logo?.Length ?? 0) > 2000)
            return ServiceError.BadRequest("value_too_long");
        var values = new Dictionary<string, string?>
        {
            [SettingKeys.BrandTitle] = req.Title?.Trim(),
            [SettingKeys.BrandDescription] = req.Description?.Trim(),
            [SettingKeys.BrandLogo] = req.Logo?.Trim(),
            [SettingKeys.BrandButtonsColor] = req.ButtonsColor,
            [SettingKeys.BrandBackgroundColor] = req.BackgroundColor,
            [SettingKeys.BrandPagesColor] = req.PagesColor,
        };
        foreach (var (k, v) in values) await UpsertAsync(k, v ?? "", ct);
        return null;
    }

    public async Task SetHomeBannerImageAsync(string? value, CancellationToken ct) =>
        await UpsertAsync(SettingKeys.HomeBannerImage, value?.Trim() ?? "", ct);

    public async Task<List<BannerDto>> BannersAsync(CancellationToken ct) =>
        Banners(await db.Settings.Find(s => s.Key.StartsWith(SettingKeys.HomeBannerPrefix)).ToListAsync(ct))
            .Select(b => new BannerDto(b.Id, b.Value)).ToList();

    public async Task<Outcome<BannerDto>> AddBannerAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return ServiceError.BadRequest("url_required");
        var all = await db.Settings.Find(s => s.Key.StartsWith(SettingKeys.HomeBannerPrefix)).ToListAsync(ct);
        var next = Banners(all).Select(b => int.Parse(b.Key[SettingKeys.HomeBannerPrefix.Length..])).DefaultIfEmpty(0).Max() + 1;
        var s = new SiteSetting { Key = $"{SettingKeys.HomeBannerPrefix}{next}", Value = url.Trim() };
        await db.Settings.InsertOneAsync(s, cancellationToken: ct);
        return new BannerDto(s.Id, s.Value);
    }

    public async Task<ServiceError?> DeleteBannerAsync(string? id, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return ServiceError.NotFound();
        var res = await db.Settings.DeleteOneAsync(s => s.Id == id && s.Key.StartsWith(SettingKeys.HomeBannerPrefix), ct);
        return res.DeletedCount == 0 ? ServiceError.NotFound() : null;
    }

    /// <summary>Stores an image under a server-generated name and returns its URL. The declared type must be one of
    /// <paramref name="allowedTypes"/> and the first bytes must really be that kind of image.</summary>
    public async Task<Outcome<string>> SaveImageAsync(byte[] bytes, string? contentType,
        IReadOnlyCollection<string> allowedTypes, HttpRequest request, CancellationToken ct)
    {
        if (bytes.Length == 0) return ServiceError.BadRequest("file_required");
        if (bytes.Length > storageOptions.Value.MaxBytes) return ServiceError.BadRequest("file_too_large");
        if (contentType is null || !allowedTypes.Contains(contentType) || !ImageTypes.TryGetValue(contentType, out var ext))
            return ServiceError.BadRequest("unsupported_type");
        if (!LooksLike(bytes, contentType)) return ServiceError.BadRequest("unsupported_type");
        using var stream = new MemoryStream(bytes, writable: false);
        return await storage.SaveAsync(stream, ext, contentType, request, ct);
    }

    /// <summary>Magic-byte check, so a file named .png that is really something else is refused.</summary>
    internal static bool LooksLike(ReadOnlySpan<byte> b, string contentType) => contentType switch
    {
        "image/png" => b.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        "image/jpeg" => b.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        "image/gif" => b.StartsWith("GIF8"u8),
        "image/webp" => b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8),
        _ => false,
    };

    /// <summary>Banner keys are home_banner_1..N (home_banner_image is a different setting).</summary>
    internal static IEnumerable<SiteSetting> Banners(IEnumerable<SiteSetting> all) =>
        all.Where(s => s.Key.StartsWith(SettingKeys.HomeBannerPrefix) && int.TryParse(s.Key[SettingKeys.HomeBannerPrefix.Length..], out _))
            .OrderBy(s => int.Parse(s.Key[SettingKeys.HomeBannerPrefix.Length..]));

    private Task UpsertAsync(string key, string value, CancellationToken ct) =>
        db.Settings.UpdateOneAsync(s => s.Key == key, Builders<SiteSetting>.Update.Set(s => s.Value, value)
            .SetOnInsert(s => s.Id, MongoDB.Bson.ObjectId.GenerateNewId().ToString()),
            new UpdateOptions { IsUpsert = true }, ct);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
