using HourOffice.Api.Data;
using HourOffice.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

public static class OfficeEndpoints
{
    public static void Map(WebApplication app)
    {
        var offices = app.MapGroup("/api/offices").RequireUser();

        // All available offices, newest first (search and capacity filters were removed — .plan/001).
        offices.MapGet("", async (MongoContext db, CancellationToken ct) =>
        {
            var list = await db.Offices.Find(o => o.IsAvailable).SortByDescending(o => o.CreatedAt).ToListAsync(ct);
            return Results.Ok(list.Select(OfficeDto.From));
        });

        offices.MapGet("/{id}", async (string id, HttpContext ctx, MongoContext db, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("office_not_found");
            var o = await db.Offices.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
            if (o is null || (!o.IsAvailable && ctx.Me().Role != Roles.Admin)) return Api.NotFound("office_not_found");
            return Results.Ok(OfficeDto.From(o));
        });

        offices.MapGet("/{id}/availability", async (string id, string? date, MongoContext db, BookingRules rules, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("office_not_found");
            if (!BookingRules.TryParseDate(date, out _)) return Api.BadRequest("invalid_date");
            var booked = await db.Bookings
                .Find(b => b.OfficeId == id && b.Date == date && b.Status == BookingStatus.Confirmed)
                .Project(b => b.Slots).ToListAsync(ct);
            return Results.Ok(new
            {
                date,
                bookedSlots = booked.SelectMany(s => s).Distinct().Order().ToList(),
                slots = rules.AllSlots,
                openHour = rules.Options.OpenHour,
                closeHour = rules.Options.CloseHour,
                today = rules.TodayText,
                nowHour = rules.LocalNow.Hour,
                maxDaysAhead = rules.Options.MaxDaysAhead,
            });
        });

        var admin = app.MapGroup("/api/admin/offices").RequireAdmin();

        admin.MapGet("", async (MongoContext db, CancellationToken ct) =>
            Results.Ok((await db.Offices.Find(FilterDefinition<Office>.Empty).SortByDescending(o => o.CreatedAt).ToListAsync(ct))
                .Select(OfficeDto.From)));

        admin.MapPost("", async (OfficeRequest req, MongoContext db, TimeProvider clock, IOptions<CurrencyOptions> cur, CancellationToken ct) =>
        {
            var err = Validate(req);
            if (err is not null) return Api.BadRequest(err);
            var o = new Office { CreatedAt = clock.GetUtcNow().UtcDateTime, Currency = cur.Value.Base };
            Apply(o, req);
            await db.Offices.InsertOneAsync(o, cancellationToken: ct);
            return Results.Created($"/api/offices/{o.Id}", OfficeDto.From(o));
        });

        admin.MapPut("/{id}", async (string id, OfficeRequest req, MongoContext db, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("office_not_found");
            var err = Validate(req);
            if (err is not null) return Api.BadRequest(err);
            var o = await db.Offices.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
            if (o is null) return Api.NotFound("office_not_found");
            var renamed = o.Name != req.Name!.Trim();
            Apply(o, req);
            await db.Offices.ReplaceOneAsync(x => x.Id == id, o, cancellationToken: ct);
            if (renamed)
                await db.Bookings.UpdateManyAsync(b => b.OfficeId == id, Builders<Booking>.Update.Set(b => b.OfficeName, o.Name), cancellationToken: ct);
            return Results.Ok(OfficeDto.From(o));
        });

        // Deleting an office cancels its future bookings; past bookings keep their history.
        admin.MapDelete("/{id}", async (string id, HttpContext ctx, MongoContext db, BookingRules rules, TimeProvider clock, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("office_not_found");
            var res = await db.Offices.DeleteOneAsync(x => x.Id == id, ct);
            if (res.DeletedCount == 0) return Api.NotFound("office_not_found");
            var today = rules.TodayText;
            await db.Bookings.UpdateManyAsync(
                b => b.OfficeId == id && b.Status == BookingStatus.Confirmed && b.Date.CompareTo(today) >= 0,
                Builders<Booking>.Update.Set(b => b.Status, BookingStatus.Cancelled)
                    .Set(b => b.CancelledAt, clock.GetUtcNow().UtcDateTime)
                    .Set(b => b.CancelledById, ctx.Me().Id), cancellationToken: ct);
            return Results.NoContent();
        });
    }

    private static string? Validate(OfficeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100) return "name_required";
        if (r.Capacity is < 1 or > 500) return "invalid_capacity";
        if (r.HourlyRate is < 0 or > 100000) return "invalid_rate";
        if ((r.Description?.Length ?? 0) > 2000) return "description_too_long";
        return null;
    }

    private static void Apply(Office o, OfficeRequest r)
    {
        o.Name = r.Name!.Trim();
        o.Description = r.Description?.Trim() ?? "";
        o.ImageUrl = r.ImageUrl?.Trim() ?? "";
        o.Capacity = r.Capacity;
        o.HourlyRate = r.HourlyRate;
        o.Amenities = (r.Amenities ?? new()).Select(a => a.Trim()).Where(a => a.Length > 0).Distinct().Take(20).ToList();
        o.Location = r.Location?.Trim() ?? "";
        o.IsAvailable = r.IsAvailable;
    }
}
