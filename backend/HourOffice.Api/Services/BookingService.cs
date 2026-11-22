using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

/// <summary>Every booking rule, used by both <see cref="BookingEndpoints"/> and the MCP tools, so the two can
/// never drift apart (plan 011, S2). The caller is always passed in; nothing here trusts a token.</summary>
public sealed class BookingService(MongoContext db, BookingRules rules, TimeProvider clock)
{
    public const int MaxNotes = 1000;

    public BookingRules Rules => rules;

    /// <summary>A user books for themself: profile + terms gates, the user window, an optional own client.</summary>
    public async Task<Outcome<MyBookingDto>> CreateAsync(User user, CreateBookingRequest req, CancellationToken ct)
    {
        var gate = await UserGates.ProfileAndTermsAsync(db, user, ct);
        if (gate is not null) return gate;
        if (!Endpoints.Api.IsObjectId(req.OfficeId)) return ServiceError.NotFound("office_not_found");
        var office = await db.Offices.Find(o => o.Id == req.OfficeId).FirstOrDefaultAsync(ct);
        if (office is null || !office.IsAvailable) return ServiceError.NotFound("office_not_found");

        var err = rules.TryBuild(req.Date, req.StartTime, req.EndTime, out var range);
        if (err is not null) return ServiceError.BadRequest(err);
        err = rules.CheckUserWindow(range!);
        if (err is not null) return ServiceError.BadRequest(err);
        if ((req.Notes?.Length ?? 0) > MaxNotes) return ServiceError.BadRequest("notes_too_long");
        Client? client = null;
        if (!string.IsNullOrWhiteSpace(req.ClientId))
        {
            client = await ClientService.FindOwnAsync(db, req.ClientId, user.Id, ct);
            if (client is null) return ServiceError.NotFound("client_not_found");
        }

        var booking = NewBooking(office, range!, user.Id, user.FullName, req.Notes, user.Id);
        booking.ClientId = client?.Id;
        try
        {
            await db.Bookings.InsertOneAsync(booking, cancellationToken: ct);
        }
        catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
        {
            return ServiceError.Conflict("slot_taken", "One or more of the selected hours were just booked.");
        }
        return MyBookingDto.From(booking, rules, client is null ? null : ClientDto.FullName(client));
    }

    /// <summary>"upcoming" = confirmed and not yet ended; everything else is "past". With
    /// <paramref name="clientId"/> only that client's bookings.</summary>
    public async Task<MyBookingsDto> MineAsync(string userId, string? clientId, CancellationToken ct)
    {
        var list = await db.Bookings.Find(b => b.UserId == userId).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(clientId)) list = list.Where(b => b.ClientId == clientId).ToList();
        var names = await ClientNamesAsync(userId, list.Select(b => b.ClientId), ct);
        MyBookingDto Dto(Booking b) =>
            MyBookingDto.From(b, rules, b.ClientId is { } c && names.TryGetValue(c, out var n) ? n : null);
        var upcoming = list.Where(b => b.Status == BookingStatus.Confirmed && !rules.HasEnded(b))
            .OrderBy(b => b.Date, StringComparer.Ordinal).ThenBy(b => b.StartTime, StringComparer.Ordinal)
            .Select(Dto).ToList();
        var past = list.Where(b => !(b.Status == BookingStatus.Confirmed && !rules.HasEnded(b)))
            .OrderByDescending(b => b.Date, StringComparer.Ordinal).ThenByDescending(b => b.StartTime, StringComparer.Ordinal)
            .Select(Dto).ToList();
        return new MyBookingsDto(upcoming, past);
    }

    /// <summary>The owner sets, changes or removes (null) the booking's client. Someone else's booking or client → 404.</summary>
    public async Task<Outcome<MyBookingDto>> SetClientAsync(User user, string? bookingId, string? clientId, CancellationToken ct)
    {
        var gate = await UserGates.TermsAsync(db, user, ct);
        if (gate is not null) return gate;
        if (!Endpoints.Api.IsObjectId(bookingId)) return ServiceError.NotFound("booking_not_found");
        var me = user.Id;
        var b = await db.Bookings.Find(x => x.Id == bookingId && x.UserId == me).FirstOrDefaultAsync(ct);
        if (b is null) return ServiceError.NotFound("booking_not_found");
        Client? client = null;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            client = await ClientService.FindOwnAsync(db, clientId, me, ct);
            if (client is null) return ServiceError.NotFound("client_not_found");
        }
        b.ClientId = client?.Id;
        await db.Bookings.UpdateOneAsync(x => x.Id == bookingId && x.UserId == me,
            Builders<Booking>.Update.Set(x => x.ClientId, b.ClientId), cancellationToken: ct);
        return MyBookingDto.From(b, rules, client is null ? null : ClientDto.FullName(client));
    }

    /// <summary>The owner (or an admin) cancels a confirmed booking. Only an admin may cancel one that has ended.</summary>
    public async Task<Outcome<BookingDto>> CancelAsync(User user, string? id, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return ServiceError.NotFound("booking_not_found");
        var b = await db.Bookings.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (b is null || (b.UserId != user.Id && user.Role != Roles.Admin)) return ServiceError.NotFound("booking_not_found");
        if (b.Status != BookingStatus.Confirmed) return ServiceError.Conflict("not_cancellable");
        if (user.Role != Roles.Admin && rules.HasEnded(b)) return ServiceError.Conflict("already_completed");
        return BookingDto.From(await MarkCancelledAsync(b, user.Id, ct), rules);
    }

    /// <summary>Upcoming first (soonest first), then past (most recent first), the original app's order.</summary>
    public async Task<List<BookingDto>> AdminListAsync(int? limit, string? userId, string? officeId, string? from,
        string? to, CancellationToken ct)
    {
        var f = Builders<Booking>.Filter;
        var filter = f.Empty;
        if (Endpoints.Api.IsObjectId(userId)) filter &= f.Eq(b => b.UserId, userId);
        if (Endpoints.Api.IsObjectId(officeId)) filter &= f.Eq(b => b.OfficeId, officeId);
        if (BookingRules.TryParseDate(from, out _)) filter &= f.Gte(b => b.Date, from);
        if (BookingRules.TryParseDate(to, out _)) filter &= f.Lte(b => b.Date, to);
        var list = await db.Bookings.Find(filter).SortByDescending(b => b.CreatedAt)
            .Limit(Math.Clamp(limit ?? 500, 1, 2000)).ToListAsync(ct);
        return Sorted(list, rules.TodayText).Select(b => BookingDto.From(b, rules)).ToList();
    }

    public async Task<List<BookingDto>> UserBookingsAsync(string userId, CancellationToken ct)
    {
        var list = await db.Bookings.Find(b => b.UserId == userId).ToListAsync(ct);
        return list.OrderByDescending(b => b.Date, StringComparer.Ordinal)
            .ThenByDescending(b => b.StartTime, StringComparer.Ordinal).Select(b => BookingDto.From(b, rules)).ToList();
    }

    public async Task<Outcome<BookingDto>> AdminUpdateAsync(string? id, AdminUpdateBookingRequest req, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return ServiceError.NotFound("booking_not_found");
        var b = await db.Bookings.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (b is null) return ServiceError.NotFound("booking_not_found");
        var officeId = req.OfficeId ?? b.OfficeId;
        if (!Endpoints.Api.IsObjectId(officeId)) return ServiceError.NotFound("office_not_found");
        var office = await db.Offices.Find(o => o.Id == officeId).FirstOrDefaultAsync(ct);
        if (office is null) return ServiceError.NotFound("office_not_found");
        var err = rules.TryBuild(req.Date ?? b.Date, req.StartTime ?? b.StartTime, req.EndTime ?? b.EndTime, out var range);
        if (err is not null) return ServiceError.BadRequest(err);
        if ((req.Notes?.Length ?? 0) > MaxNotes) return ServiceError.BadRequest("notes_too_long");

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
            return ServiceError.Conflict("slot_taken", "The selected time overlaps another booking.");
        }
        return BookingDto.From(b, rules);
    }

    public async Task<Outcome<BookingDto>> AdminCancelAsync(User admin, string? id, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return ServiceError.NotFound("booking_not_found");
        var b = await db.Bookings.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (b is null) return ServiceError.NotFound("booking_not_found");
        if (b.Status != BookingStatus.Confirmed) return ServiceError.Conflict("not_cancellable");
        return BookingDto.From(await MarkCancelledAsync(b, admin.Id, ct), rules);
    }

    /// <summary>Number of bookings a recurring request would create (before taken hours are skipped), or an error.
    /// Used by the MCP confirmation step.</summary>
    public Outcome<int> CountRecurring(RecurringBookingRequest req, int maxDays)
    {
        var err = ValidateRecurring(req, maxDays, out var from, out var to, out var days);
        if (err is not null) return err;
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (days.Contains((int)d.DayOfWeek)) n++;
        return n;
    }

    /// <summary>One booking per matching weekday in [fromDate, toDate]; hours that are already taken are skipped.</summary>
    public async Task<Outcome<RecurringResultDto>> RecurringAsync(User admin, RecurringBookingRequest req, int maxDays,
        CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(req.OfficeId)) return ServiceError.NotFound("office_not_found");
        var office = await db.Offices.Find(o => o.Id == req.OfficeId).FirstOrDefaultAsync(ct);
        if (office is null) return ServiceError.NotFound("office_not_found");
        var err = ValidateRecurring(req, maxDays, out var from, out var to, out var days);
        if (err is not null) return err;
        if ((req.Notes?.Length ?? 0) > MaxNotes) return ServiceError.BadRequest("notes_too_long");

        var owner = admin;
        if (Endpoints.Api.IsObjectId(req.UserId))
        {
            owner = await db.Users.Find(u => u.Id == req.UserId).FirstOrDefaultAsync(ct);
            if (owner is null) return ServiceError.NotFound("user_not_found");
        }
        var bookerName = string.IsNullOrWhiteSpace(req.BookerName) ? owner.FullName : req.BookerName.Trim();
        var seriesId = ObjectId.GenerateNewId().ToString();

        var docs = new List<Booking>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!days.Contains((int)d.DayOfWeek)) continue;
            rules.TryBuild(d.ToString("yyyy-MM-dd"), req.StartTime, req.EndTime, out var r);
            var b = NewBooking(office, r!, owner.Id, bookerName, req.Notes, admin.Id);
            b.SeriesId = seriesId;
            docs.Add(b);
        }
        if (docs.Count == 0) return new RecurringResultDto(0, 0, seriesId);

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
        return new RecurringResultDto(created, docs.Count - created, seriesId);
    }

    private ServiceError? ValidateRecurring(RecurringBookingRequest req, int maxDays, out DateOnly from, out DateOnly to,
        out HashSet<int> days)
    {
        days = (req.Days ?? new()).Where(d => d is >= 0 and <= 6).ToHashSet();
        to = default;
        if (!BookingRules.TryParseDate(req.FromDate, out from) || !BookingRules.TryParseDate(req.ToDate, out to))
            return ServiceError.BadRequest("invalid_date");
        if (to < from) return ServiceError.BadRequest("end_before_start");
        if (to.DayNumber - from.DayNumber > maxDays) return ServiceError.BadRequest("range_too_long");
        if (days.Count == 0) return ServiceError.BadRequest("days_required");
        var err = rules.TryBuild(req.FromDate, req.StartTime, req.EndTime, out _);
        return err is null ? null : ServiceError.BadRequest(err);
    }

    private Booking NewBooking(Office office, SlotRange r, string userId, string bookerName, string? notes,
        string createdById) => new()
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

    private async Task<Booking> MarkCancelledAsync(Booking b, string byUserId, CancellationToken ct)
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
    private async Task<Dictionary<string, string>> ClientNamesAsync(string ownerId, IEnumerable<string?> clientIds,
        CancellationToken ct)
    {
        var ids = clientIds.OfType<string>().Distinct().ToList();
        if (ids.Count == 0) return new();
        var clients = await db.Clients.Find(c => ids.Contains(c.Id) && c.UserId == ownerId).ToListAsync(ct);
        return clients.ToDictionary(c => c.Id, ClientDto.FullName);
    }
}
