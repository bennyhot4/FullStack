using HourOffice.Api.Data;
using HourOffice.Api.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

public static class BookingEndpoints
{
    public static void Map(WebApplication app)
    {
        var bookings = app.MapGroup("/api/bookings").RequireUser();

        bookings.MapPost("", async (CreateBookingRequest req, HttpContext ctx, MongoContext db, BookingRules rules,
            TimeProvider clock, CancellationToken ct) =>
        {
            var user = ctx.Me();
            if (!Api.IsObjectId(req.OfficeId)) return Api.NotFound("office_not_found");
            var office = await db.Offices.Find(o => o.Id == req.OfficeId).FirstOrDefaultAsync(ct);
            if (office is null || !office.IsAvailable) return Api.NotFound("office_not_found");

            var err = rules.TryBuild(req.Date, req.StartTime, req.EndTime, out var range);
            if (err is not null) return Api.BadRequest(err);
            err = rules.CheckUserWindow(range!);
            if (err is not null) return Api.BadRequest(err);
            if ((req.Notes?.Length ?? 0) > 1000) return Api.BadRequest("notes_too_long");
            Client? client = null;
            if (!string.IsNullOrWhiteSpace(req.ClientId))
            {
                client = await ClientEndpoints.FindOwnAsync(db, req.ClientId, user.Id, ct);
                if (client is null) return Api.NotFound("client_not_found");
            }

            var booking = NewBooking(office, range!, user.Id, user.FullName, req.Notes, user.Id, clock);
            booking.ClientId = client?.Id;
            try
            {
                await db.Bookings.InsertOneAsync(booking, cancellationToken: ct);
            }
            catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
            {
                return Api.Conflict("slot_taken", "One or more of the selected hours were just booked.");
            }
            return Results.Created($"/api/bookings/{booking.Id}",
                MyBookingDto.From(booking, rules, client is null ? null : ClientDto.FullName(client)));
        }).AddEndpointFilter(async (ctx, next) =>
            string.IsNullOrWhiteSpace(ctx.HttpContext.Me().PhoneNumber) ? Api.Forbidden("profile_required") : await next(ctx))
          .RequireTerms();

        bookings.MapGet("/mine", async (HttpContext ctx, MongoContext db, BookingRules rules, CancellationToken ct) =>
        {
            var id = ctx.Me().Id;
            var list = await db.Bookings.Find(b => b.UserId == id).ToListAsync(ct);
            var names = await ClientNamesAsync(db, id, list.Select(b => b.ClientId), ct);
            return Results.Ok(Split(list, rules, names));
        });

        // The owner sets, changes or removes (clientId: null) the booking's client. Cancelled bookings too:
        // it is only a label. Someone else's booking or client → 404.
        bookings.MapPut("/{id}/client", async (string id, SetBookingClientRequest req, HttpContext ctx, MongoContext db,
            BookingRules rules, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("booking_not_found");
            var me = ctx.Me().Id;
            var b = await db.Bookings.Find(x => x.Id == id && x.UserId == me).FirstOrDefaultAsync(ct);
            if (b is null) return Api.NotFound("booking_not_found");
            Client? client = null;
            if (!string.IsNullOrWhiteSpace(req.ClientId))
            {
                client = await ClientEndpoints.FindOwnAsync(db, req.ClientId, me, ct);
                if (client is null) return Api.NotFound("client_not_found");
            }
            b.ClientId = client?.Id;
            await db.Bookings.UpdateOneAsync(x => x.Id == id && x.UserId == me,
                Builders<Booking>.Update.Set(x => x.ClientId, b.ClientId), cancellationToken: ct);
            return Results.Ok(MyBookingDto.From(b, rules, client is null ? null : ClientDto.FullName(client)));
        }).RequireTerms();

        bookings.MapPost("/{id}/cancel", async (string id, HttpContext ctx, MongoContext db, BookingRules rules,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("booking_not_found");
            var user = ctx.Me();
            var b = await db.Bookings.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
            if (b is null || (b.UserId != user.Id && user.Role != Roles.Admin)) return Api.NotFound("booking_not_found");
            if (b.Status != BookingStatus.Confirmed) return Api.Conflict("not_cancellable");
            if (user.Role != Roles.Admin && rules.HasEnded(b)) return Api.Conflict("already_completed");
            return Results.Ok(BookingDto.From(await CancelAsync(db, b, user.Id, clock, ct), rules));
        });

        var admin = app.MapGroup("/api/admin/bookings").RequireAdmin();

        // Upcoming first (soonest first), then past (most recent first) – same order as the original app.
        admin.MapGet("", async (int? limit, string? userId, MongoContext db, BookingRules rules, CancellationToken ct) =>
        {
            var filter = Api.IsObjectId(userId)
                ? Builders<Booking>.Filter.Eq(b => b.UserId, userId)
                : FilterDefinition<Booking>.Empty;
            var list = await db.Bookings.Find(filter).SortByDescending(b => b.CreatedAt)
                .Limit(Math.Clamp(limit ?? 500, 1, 2000)).ToListAsync(ct);
            return Results.Ok(Sorted(list, rules.TodayText).Select(b => BookingDto.From(b, rules)));
        });

        admin.MapPut("/{id}", async (string id, AdminUpdateBookingRequest req, MongoContext db, BookingRules rules, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("booking_not_found");
            var b = await db.Bookings.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
            if (b is null) return Api.NotFound("booking_not_found");
            var officeId = req.OfficeId ?? b.OfficeId;
            if (!Api.IsObjectId(officeId)) return Api.NotFound("office_not_found");
            var office = await db.Offices.Find(o => o.Id == officeId).FirstOrDefaultAsync(ct);
            if (office is null) return Api.NotFound("office_not_found");
            var err = rules.TryBuild(req.Date ?? b.Date, req.StartTime ?? b.StartTime, req.EndTime ?? b.EndTime, out var range);
            if (err is not null) return Api.BadRequest(err);

            b.OfficeId = office.Id;
            b.OfficeName = office.Name;
            b.BookerName = string.IsNullOrWhiteSpace(req.BookerName) ? b.BookerName : req.BookerName.Trim();
            b.Notes = req.Notes ?? b.Notes;
            b.Date = range!.DateText;
            b.StartTime = range.StartTime;
            b.EndTime = range.EndTime;
            b.Slots = range.Slots;
            b.DurationHours = range.Hours;
            b.TotalPrice = range.Hours * office.HourlyRate;
            b.Currency = office.Currency;
            try
            {
                await db.Bookings.ReplaceOneAsync(x => x.Id == id, b, cancellationToken: ct);
            }
            catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
            {
                return Api.Conflict("slot_taken", "The selected time overlaps another booking.");
            }
            return Results.Ok(BookingDto.From(b, rules));
        });

        admin.MapPost("/{id}/cancel", async (string id, HttpContext ctx, MongoContext db, BookingRules rules, TimeProvider clock, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("booking_not_found");
            var b = await db.Bookings.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
            if (b is null) return Api.NotFound("booking_not_found");
            if (b.Status != BookingStatus.Confirmed) return Api.Conflict("not_cancellable");
            return Results.Ok(BookingDto.From(await CancelAsync(db, b, ctx.Me().Id, clock, ct), rules));
        });

        // Creates one booking per matching weekday in [fromDate, toDate]; hours that are already taken are skipped.
        admin.MapPost("/recurring", async (RecurringBookingRequest req, HttpContext ctx, MongoContext db, BookingRules rules,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(req.OfficeId)) return Api.NotFound("office_not_found");
            var office = await db.Offices.Find(o => o.Id == req.OfficeId).FirstOrDefaultAsync(ct);
            if (office is null) return Api.NotFound("office_not_found");
            if (!BookingRules.TryParseDate(req.FromDate, out var from) || !BookingRules.TryParseDate(req.ToDate, out var to))
                return Api.BadRequest("invalid_date");
            if (to < from) return Api.BadRequest("end_before_start");
            if (to.DayNumber - from.DayNumber > rules.Options.RecurringMaxDays) return Api.BadRequest("range_too_long");
            var days = (req.Days ?? new()).Where(d => d is >= 0 and <= 6).ToHashSet();
            if (days.Count == 0) return Api.BadRequest("days_required");
            var err = rules.TryBuild(req.FromDate, req.StartTime, req.EndTime, out _);
            if (err is not null) return Api.BadRequest(err);

            var owner = ctx.Me();
            if (Api.IsObjectId(req.UserId))
            {
                owner = await db.Users.Find(u => u.Id == req.UserId).FirstOrDefaultAsync(ct);
                if (owner is null) return Api.NotFound("user_not_found");
            }
            var bookerName = string.IsNullOrWhiteSpace(req.BookerName) ? owner.FullName : req.BookerName.Trim();
            var seriesId = ObjectId.GenerateNewId().ToString();

            var docs = new List<Booking>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (!days.Contains((int)d.DayOfWeek)) continue;
                rules.TryBuild(d.ToString("yyyy-MM-dd"), req.StartTime, req.EndTime, out var r);
                var b = NewBooking(office, r!, owner.Id, bookerName, req.Notes, ctx.Me().Id, clock);
                b.SeriesId = seriesId;
                docs.Add(b);
            }
            if (docs.Count == 0) return Results.Ok(new { created = 0, skipped = 0, seriesId });

            var created = docs.Count;
            try
            {
                await db.Bookings.InsertManyAsync(docs, new InsertManyOptions { IsOrdered = false }, ct);
            }
            catch (MongoBulkWriteException<Booking> ex)
            {
                if (ex.WriteErrors.Any(e => e.Category != ServerErrorCategory.DuplicateKey)) throw;
                created -= ex.WriteErrors.Count;
            }
            return Results.Ok(new { created, skipped = docs.Count - created, seriesId });
        });
    }

    internal static Booking NewBooking(Office office, SlotRange r, string userId, string bookerName, string? notes,
        string createdById, TimeProvider clock) => new()
    {
        OfficeId = office.Id,
        OfficeName = office.Name,
        UserId = userId,
        BookerName = bookerName,
        Date = r.DateText,
        StartTime = r.StartTime,
        EndTime = r.EndTime,
        Slots = r.Slots,
        DurationHours = r.Hours,
        TotalPrice = r.Hours * office.HourlyRate,
        Currency = office.Currency,
        Status = BookingStatus.Confirmed,
        Notes = notes?.Trim() ?? "",
        CreatedById = createdById,
        CreatedAt = clock.GetUtcNow().UtcDateTime,
    };

    private static async Task<Booking> CancelAsync(MongoContext db, Booking b, string byUserId, TimeProvider clock, CancellationToken ct)
    {
        b.Status = BookingStatus.Cancelled;
        b.CancelledAt = clock.GetUtcNow().UtcDateTime;
        b.CancelledById = byUserId;
        await db.Bookings.UpdateOneAsync(x => x.Id == b.Id && x.Status == BookingStatus.Confirmed, Builders<Booking>.Update
            .Set(x => x.Status, b.Status).Set(x => x.CancelledAt, b.CancelledAt).Set(x => x.CancelledById, b.CancelledById),
            cancellationToken: ct);
        return b;
    }

    internal static IEnumerable<Booking> Sorted(IEnumerable<Booking> list, string today)
    {
        var all = list.ToList();
        var upcoming = all.Where(b => string.CompareOrdinal(b.Date, today) >= 0)
            .OrderBy(b => b.Date, StringComparer.Ordinal).ThenBy(b => b.StartTime, StringComparer.Ordinal);
        var past = all.Where(b => string.CompareOrdinal(b.Date, today) < 0)
            .OrderByDescending(b => b.Date, StringComparer.Ordinal).ThenByDescending(b => b.StartTime, StringComparer.Ordinal);
        return upcoming.Concat(past);
    }

    /// <summary>Names of the owner's clients linked to these bookings, in one query. The owner filter means a
    /// clientId that is not (or no longer) the owner's simply gets no name.</summary>
    private static async Task<Dictionary<string, string>> ClientNamesAsync(MongoContext db, string ownerId,
        IEnumerable<string?> clientIds, CancellationToken ct)
    {
        var ids = clientIds.OfType<string>().Distinct().ToList();
        if (ids.Count == 0) return new();
        var clients = await db.Clients.Find(c => ids.Contains(c.Id) && c.UserId == ownerId).ToListAsync(ct);
        return clients.ToDictionary(c => c.Id, ClientDto.FullName);
    }

    /// <summary>"upcoming" = confirmed and not yet ended; everything else is "past".</summary>
    private static object Split(List<Booking> list, BookingRules rules, Dictionary<string, string> clientNames)
    {
        MyBookingDto Dto(Booking b) =>
            MyBookingDto.From(b, rules, b.ClientId is { } c && clientNames.TryGetValue(c, out var n) ? n : null);
        var upcoming = list.Where(b => b.Status == BookingStatus.Confirmed && !rules.HasEnded(b))
            .OrderBy(b => b.Date, StringComparer.Ordinal).ThenBy(b => b.StartTime, StringComparer.Ordinal)
            .Select(Dto).ToList();
        var past = list.Where(b => !(b.Status == BookingStatus.Confirmed && !rules.HasEnded(b)))
            .OrderByDescending(b => b.Date, StringComparer.Ordinal).ThenByDescending(b => b.StartTime, StringComparer.Ordinal)
            .Select(Dto).ToList();
        return new { upcoming, past };
    }
}
