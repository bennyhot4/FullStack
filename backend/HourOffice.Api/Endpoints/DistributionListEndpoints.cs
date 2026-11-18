using HourOffice.Api.Data;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

/// <summary>A user's private distribution lists: named groups of their own clients, for future announcements.
/// Same privacy rules as <see cref="ClientEndpoints"/>: the owner filter is inside every MongoDB filter, someone
/// else's list is 404, and there is no admin route. Open to every user; the site shows it to admins only for now.</summary>
public static class DistributionListEndpoints
{
    public static void Map(WebApplication app)
    {
        var lists = app.MapGroup("/api/distribution-lists").RequireUser().RequireTerms();

        lists.MapGet("", async (HttpContext ctx, MongoContext db, CancellationToken ct) =>
        {
            var me = ctx.Me().Id;
            var list = await db.DistributionLists.Find(l => l.UserId == me)
                .SortBy(l => l.Name).Limit(DistributionList.MaxPerUser).ToListAsync(ct);
            return Results.Ok(list.Select(DistributionListSummaryDto.From));
        });

        lists.MapPost("", async (DistributionListRequest req, HttpContext ctx, MongoContext db, TimeProvider clock,
            CancellationToken ct) =>
        {
            var me = ctx.Me().Id;
            var (err, v) = await ReadAsync(db, req, me, ct);
            if (err is not null) return err;
            if (await db.DistributionLists.CountDocumentsAsync(l => l.UserId == me, cancellationToken: ct) >= DistributionList.MaxPerUser)
                return Api.Conflict("list_limit");

            var now = clock.GetUtcNow().UtcDateTime;
            var list = new DistributionList
            {
                UserId = me, Name = v!.Name, Description = v.Description, ClientIds = v.ClientIds,
                CreatedAt = now, UpdatedAt = now,
            };
            await db.DistributionLists.InsertOneAsync(list, cancellationToken: ct);
            return Results.Created($"/api/distribution-lists/{list.Id}", DistributionListDto.From(list, v.Members));
        });

        lists.MapGet("/{id}", async (string id, HttpContext ctx, MongoContext db, CancellationToken ct) =>
        {
            var me = ctx.Me().Id;
            var list = await FindOwnAsync(db, id, me, ct);
            if (list is null) return Api.NotFound("distribution_list_not_found");
            return Results.Ok(DistributionListDto.From(list, await MembersAsync(db, list.ClientIds, me, ct)));
        });

        lists.MapPut("/{id}", async (string id, DistributionListRequest req, HttpContext ctx, MongoContext db,
            TimeProvider clock, CancellationToken ct) =>
        {
            var me = ctx.Me().Id;
            var list = await FindOwnAsync(db, id, me, ct);
            if (list is null) return Api.NotFound("distribution_list_not_found");
            var (err, v) = await ReadAsync(db, req, me, ct);
            if (err is not null) return err;

            list.Name = v!.Name;
            list.Description = v.Description;
            list.ClientIds = v.ClientIds;
            list.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            var res = await db.DistributionLists.ReplaceOneAsync(l => l.Id == id && l.UserId == me, list, cancellationToken: ct);
            return res.MatchedCount == 0
                ? Api.NotFound("distribution_list_not_found")
                : Results.Ok(DistributionListDto.From(list, v.Members));
        });

        // Deletes the list only; its clients stay.
        lists.MapDelete("/{id}", async (string id, HttpContext ctx, MongoContext db, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("distribution_list_not_found");
            var me = ctx.Me().Id;
            var res = await db.DistributionLists.DeleteOneAsync(l => l.Id == id && l.UserId == me, ct);
            return res.DeletedCount == 0 ? Api.NotFound("distribution_list_not_found") : Results.NoContent();
        });
    }

    private static async Task<DistributionList?> FindOwnAsync(MongoContext db, string id, string userId, CancellationToken ct) =>
        Api.IsObjectId(id) ? await db.DistributionLists.Find(l => l.Id == id && l.UserId == userId).FirstOrDefaultAsync(ct) : null;

    /// <summary>The owner's clients among <paramref name="ids"/>, sorted by name. Ids that are gone are dropped.</summary>
    private static async Task<List<Client>> MembersAsync(MongoContext db, List<string> ids, string userId, CancellationToken ct) =>
        ids.Count == 0
            ? new List<Client>()
            : await db.Clients.Find(c => ids.Contains(c.Id) && c.UserId == userId)
                .SortBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync(ct);

    private sealed record Values(string Name, string Description, List<string> ClientIds, List<Client> Members);

    /// <summary>Validates a request. Every client id must be one of the owner's clients (checked in one query).</summary>
    private static async Task<(IResult? Error, Values? Value)> ReadAsync(MongoContext db, DistributionListRequest req,
        string userId, CancellationToken ct)
    {
        var name = req.Name?.Trim() ?? "";
        if (name.Length is 0 || name.Length > DistributionList.MaxName) return (Api.BadRequest("list_name_required"), null);
        var description = req.Description?.Trim() ?? "";
        if (description.Length > DistributionList.MaxDescription) return (Api.BadRequest("list_description_too_long"), null);

        var ids = (req.ClientIds ?? new()).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!.Trim()).Distinct().ToList();
        if (ids.Count > DistributionList.MaxClients) return (Api.BadRequest("too_many_list_clients"), null);
        if (ids.Any(i => !Api.IsObjectId(i))) return (Api.NotFound("client_not_found"), null);

        var members = await MembersAsync(db, ids, userId, ct);
        if (members.Count != ids.Count) return (Api.NotFound("client_not_found"), null);
        // Keep the order the user picked them in.
        return (null, new Values(name, description, ids, members));
    }
}
