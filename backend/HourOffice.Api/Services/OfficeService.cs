using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

/// <summary>Office rules shared by <see cref="OfficeEndpoints"/> and the MCP tools.</summary>
public sealed class OfficeService(MongoContext db, BookingRules rules, TimeProvider clock, IOptions<CurrencyOptions> currency)
{
    /// <summary>All available offices, newest first (search and capacity filters were removed — .plan/001).</summary>
    public async Task<List<OfficeDto>> ListAvailableAsync(CancellationToken ct) =>
        (await db.Offices.Find(o => o.IsAvailable).SortByDescending(o => o.CreatedAt).ToListAsync(ct))
            .Select(OfficeDto.From).ToList();

    public async Task<List<OfficeDto>> AdminListAsync(CancellationToken ct) =>
        (await db.Offices.Find(FilterDefinition<Office>.Empty).SortByDescending(o => o.CreatedAt).ToListAsync(ct))
            .Select(OfficeDto.From).ToList();

    /// <summary>An unavailable office is visible to admins only.</summary>
    public async Task<Outcome<OfficeDto>> GetAsync(User user, string? id, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return ServiceError.NotFound("office_not_found");
        var o = await db.Offices.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (o is null || (!o.IsAvailable && user.Role != Roles.Admin)) return ServiceError.NotFound("office_not_found");
        return OfficeDto.From(o);
    }

    /// <summary>Booked hours of one day. Never says who booked them.</summary>
    public async Task<Outcome<AvailabilityDto>> AvailabilityAsync(string? id, string? date, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return ServiceError.NotFound("office_not_found");
        if (!BookingRules.TryParseDate(date, out _)) return ServiceError.BadRequest("invalid_date");
        var booked = await db.Bookings
            .Find(b => b.OfficeId == id && b.Date == date && b.Status == BookingStatus.Confirmed)
            .Project(b => b.Slots).ToListAsync(ct);
        return new AvailabilityDto(date!, booked.SelectMany(s => s).Distinct().Order().ToList(), rules.AllSlots,
            rules.Options.OpenHour, rules.Options.CloseHour, rules.TodayText, rules.LocalNow.Hour, rules.Options.MaxDaysAhead);
    }

    public async Task<Outcome<OfficeDto>> CreateAsync(OfficeRequest req, CancellationToken ct)
    {
        var err = Validate(req);
        if (err is not null) return ServiceError.BadRequest(err);
        var o = new Office { CreatedAt = clock.GetUtcNow().UtcDateTime, Currency = currency.Value.Base };
        Apply(o, req);
        await db.Offices.InsertOneAsync(o, cancellationToken: ct);
        return OfficeDto.From(o);
    }

    public async Task<Outcome<OfficeDto>> UpdateAsync(string? id, OfficeRequest req, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return ServiceError.NotFound("office_not_found");
        var err = Validate(req);
        if (err is not null) return ServiceError.BadRequest(err);
        var o = await db.Offices.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (o is null) return ServiceError.NotFound("office_not_found");
        var renamed = o.Name != req.Name!.Trim();
        Apply(o, req);
        await db.Offices.ReplaceOneAsync(x => x.Id == id, o, cancellationToken: ct);
        if (renamed)
            await db.Bookings.UpdateManyAsync(b => b.OfficeId == id, Builders<Booking>.Update.Set(b => b.OfficeName, o.Name),
                cancellationToken: ct);
        return OfficeDto.From(o);
    }

    /// <summary>The office as an <see cref="OfficeRequest"/>, so a partial change can be applied on top of it.</summary>
    public async Task<OfficeRequest?> CurrentAsRequestAsync(string? id, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return null;
        var o = await db.Offices.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        return o is null ? null
            : new OfficeRequest(o.Name, o.Description, o.ImageUrl, o.Capacity, o.HourlyRate, o.Amenities, o.Location, o.IsAvailable);
    }

    /// <summary>Deleting an office cancels its future bookings; past bookings keep their history. Site only.</summary>
    public async Task<ServiceError?> DeleteAsync(User admin, string? id, CancellationToken ct)
    {
        if (!Endpoints.Api.IsObjectId(id)) return ServiceError.NotFound("office_not_found");
        var res = await db.Offices.DeleteOneAsync(x => x.Id == id, ct);
        if (res.DeletedCount == 0) return ServiceError.NotFound("office_not_found");
        var today = rules.TodayText;
        await db.Bookings.UpdateManyAsync(
            b => b.OfficeId == id && b.Status == BookingStatus.Confirmed && b.Date.CompareTo(today) >= 0,
            Builders<Booking>.Update.Set(b => b.Status, BookingStatus.Cancelled)
                .Set(b => b.CancelledAt, clock.GetUtcNow().UtcDateTime)
                .Set(b => b.CancelledById, admin.Id), cancellationToken: ct);
        return null;
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
