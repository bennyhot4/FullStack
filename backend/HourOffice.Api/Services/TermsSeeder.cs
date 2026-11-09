using HourOffice.Api.Data;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

/// <summary>On first start, publishes the terms of use that existed in the original app as version 1.</summary>
public static class TermsSeeder
{
    public static async Task SeedAsync(MongoContext db, IWebHostEnvironment env, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Terms.Find(FilterDefinition<TermsDoc>.Empty).AnyAsync(ct)) return;
        var dir = Path.Combine(AppContext.BaseDirectory, "Seed");
        if (!Directory.Exists(dir)) dir = Path.Combine(env.ContentRootPath, "Seed");
        var he = Path.Combine(dir, "terms.he.md");
        var en = Path.Combine(dir, "terms.en.md");
        if (!File.Exists(he) || !File.Exists(en)) return;
        try
        {
            await db.Terms.InsertOneAsync(new TermsDoc
            {
                Version = 1,
                ContentHe = await File.ReadAllTextAsync(he, ct),
                ContentEn = await File.ReadAllTextAsync(en, ct),
                PublishedAt = clock.GetUtcNow().UtcDateTime,
            }, cancellationToken: ct);
        }
        catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
        {
            // Another instance seeded it first.
        }
    }
}
