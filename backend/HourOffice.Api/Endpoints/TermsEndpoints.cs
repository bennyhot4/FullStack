using HourOffice.Api.Data;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

public static class TermsEndpoints
{
    public static async Task<int> CurrentVersionAsync(MongoContext db, CancellationToken ct) =>
        await db.Terms.Find(FilterDefinition<TermsDoc>.Empty).SortByDescending(t => t.Version)
            .Project(t => t.Version).FirstOrDefaultAsync(ct);

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/terms/current", async (MongoContext db, CancellationToken ct) =>
        {
            var t = await db.Terms.Find(FilterDefinition<TermsDoc>.Empty).SortByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            return t is null
                ? Api.NotFound("no_terms")
                : Results.Ok(new { t.Version, t.ContentHe, t.ContentEn, t.PublishedAt });
        });

        app.MapGroup("/api/admin/terms").RequireAdmin().MapPut("", async (TermsRequest req, HttpContext ctx,
            MongoContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.ContentHe) || string.IsNullOrWhiteSpace(req.ContentEn))
                return Api.BadRequest("content_required");
            var next = await CurrentVersionAsync(db, ct) + 1;
            try
            {
                await db.Terms.InsertOneAsync(new TermsDoc
                {
                    Version = next,
                    ContentHe = req.ContentHe,
                    ContentEn = req.ContentEn,
                    PublishedAt = clock.GetUtcNow().UtcDateTime,
                    PublishedById = ctx.Me().Id,
                }, cancellationToken: ct);
            }
            catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
            {
                return Api.Conflict("concurrent_update");
            }
            return Results.Ok(new { version = next });
        });
    }
}
