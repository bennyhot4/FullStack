using System.Text.RegularExpressions;
using HourOffice.Api.Data;
using HourOffice.Api.Services;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

/// <summary>A user's private client list. Every query filters by the signed-in user's id inside the
/// MongoDB filter, and someone else's client is reported as 404 (never 403) so its id is not revealed.
/// There is deliberately no admin route.</summary>
public static partial class ClientEndpoints
{
    public static void Map(WebApplication app)
    {
        var clients = app.MapGroup("/api/clients").RequireUser().RequireTerms();

        clients.MapGet("", async (HttpContext ctx, MongoContext db, CancellationToken ct) =>
        {
            var me = ctx.Me().Id;
            var list = await db.Clients.Find(c => c.UserId == me)
                .SortBy(c => c.LastName).ThenBy(c => c.FirstName).Limit(Client.MaxPerUser).ToListAsync(ct);
            return Results.Ok(list.Select(ClientDto.From));
        });

        clients.MapPost("", async (ClientRequest req, HttpContext ctx, MongoContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var me = ctx.Me().Id;
            var err = TryRead(req, out var v);
            if (err is not null) return Api.BadRequest(err);
            if (await db.Clients.CountDocumentsAsync(c => c.UserId == me, cancellationToken: ct) >= Client.MaxPerUser)
                return Api.Conflict("client_limit");

            var now = clock.GetUtcNow().UtcDateTime;
            var client = new Client { UserId = me, CreatedAt = now };
            Apply(client, v!, now);
            await db.Clients.InsertOneAsync(client, cancellationToken: ct);
            return Results.Created($"/api/clients/{client.Id}", ClientDto.From(client));
        });

        clients.MapGet("/{id}", async (string id, HttpContext ctx, MongoContext db, CancellationToken ct) =>
        {
            var client = await FindOwnAsync(db, id, ctx.Me().Id, ct);
            return client is null ? Api.NotFound("client_not_found") : Results.Ok(ClientDto.From(client));
        });

        clients.MapPut("/{id}", async (string id, ClientRequest req, HttpContext ctx, MongoContext db, TimeProvider clock,
            CancellationToken ct) =>
        {
            var me = ctx.Me().Id;
            var client = await FindOwnAsync(db, id, me, ct);
            if (client is null) return Api.NotFound("client_not_found");
            var err = TryRead(req, out var v);
            if (err is not null) return Api.BadRequest(err);

            Apply(client, v!, clock.GetUtcNow().UtcDateTime);
            var res = await db.Clients.ReplaceOneAsync(c => c.Id == id && c.UserId == me, client, cancellationToken: ct);
            return res.MatchedCount == 0 ? Api.NotFound("client_not_found") : Results.Ok(ClientDto.From(client));
        });

        // Bookings linked to a deleted client keep their clientId; they just show no name. It leaves the owner's lists.
        clients.MapDelete("/{id}", async (string id, HttpContext ctx, MongoContext db, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("client_not_found");
            var me = ctx.Me().Id;
            var res = await db.Clients.DeleteOneAsync(c => c.Id == id && c.UserId == me, ct);
            if (res.DeletedCount == 0) return Api.NotFound("client_not_found");
            await db.DistributionLists.UpdateManyAsync(l => l.UserId == me,
                Builders<DistributionList>.Update.Pull(l => l.ClientIds, id), cancellationToken: ct);
            return Results.NoContent();
        });
    }

    /// <summary>The client only if it belongs to <paramref name="userId"/>; null otherwise.</summary>
    internal static async Task<Client?> FindOwnAsync(MongoContext db, string? id, string userId, CancellationToken ct) =>
        Api.IsObjectId(id) ? await db.Clients.Find(c => c.Id == id && c.UserId == userId).FirstOrDefaultAsync(ct) : null;

    private sealed record Values(string FirstName, string LastName, List<string> Phones, List<string> Emails,
        string Language, bool Consent, string Notes);

    /// <summary>Validates and normalizes a request. Returns an error code, or null with <paramref name="v"/> set.</summary>
    private static string? TryRead(ClientRequest req, out Values? v)
    {
        v = null;
        var first = req.FirstName?.Trim() ?? "";
        var last = req.LastName?.Trim() ?? "";
        if (first.Length is 0 or > 60) return "client_name_required";
        if (last.Length > 60) return "client_name_required";

        var rawPhones = (req.Phones ?? new()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (rawPhones.Count == 0) return "phone_required";
        if (rawPhones.Count > Client.MaxPhones) return "too_many_phones";
        var phones = new List<string>();
        foreach (var raw in rawPhones)
        {
            var p = Phone.Normalize(raw);
            if (p is null) return "invalid_phone";
            if (!phones.Contains(p)) phones.Add(p);
        }

        var rawEmails = (req.Emails ?? new()).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
        if (rawEmails.Count > Client.MaxEmails) return "too_many_emails";
        var emails = new List<string>();
        foreach (var raw in rawEmails)
        {
            var e = raw!.Trim().ToLowerInvariant();
            if (e.Length > 200 || !EmailShape().IsMatch(e)) return "invalid_email";
            if (!emails.Contains(e)) emails.Add(e);
        }

        var language = string.IsNullOrWhiteSpace(req.Language) ? "he" : req.Language.Trim();
        if (!Client.IsLanguage(language)) return "invalid_language";
        var notes = req.Notes?.Trim() ?? "";
        if (notes.Length > Client.MaxNotes) return "notes_too_long";

        v = new Values(first, last, phones, emails, language, req.WhatsAppConsent, notes);
        return null;
    }

    private static void Apply(Client c, Values v, DateTime now)
    {
        c.FirstName = v.FirstName;
        c.LastName = v.LastName;
        c.Phones = v.Phones;
        c.Emails = v.Emails;
        c.Language = v.Language;
        // The server records when consent was given; turning it off clears the time.
        if (v.Consent && !c.WhatsAppConsent) c.WhatsAppConsentAt = now;
        if (!v.Consent) c.WhatsAppConsentAt = null;
        c.WhatsAppConsent = v.Consent;
        c.Notes = v.Notes;
        c.UpdatedAt = now;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailShape();
}
