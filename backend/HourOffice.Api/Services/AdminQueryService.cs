using System.Globalization;
using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

/// <summary>Admin reads (users list, analytics) shared by <see cref="AdminEndpoints"/> and the MCP tools.</summary>
public sealed class AdminQueryService(MongoContext db, BookingRules rules)
{
    public async Task<List<UserDto>> UsersAsync(CancellationToken ct)
    {
        var version = await TermsEndpoints.CurrentVersionAsync(db, ct);
        var list = await db.Users.Find(FilterDefinition<User>.Empty).SortByDescending(u => u.CreatedAt).ToListAsync(ct);
        return list.Select(u => UserDto.From(u, version)).ToList();
    }

    public async Task<bool> UserExistsAsync(string? id, CancellationToken ct) =>
        Endpoints.Api.IsObjectId(id) && await db.Users.Find(u => u.Id == id).AnyAsync(ct);

    /// <summary>Week starts on Monday. Only confirmed (incl. completed) bookings count; admins are excluded from the
    /// per-user list.</summary>
    public async Task<AnalyticsDto> AnalyticsAsync(string? period, CancellationToken ct)
    {
        var today = rules.Today;
        DateOnly from, to;
        if (period == "month")
        {
            from = new DateOnly(today.Year, today.Month, 1);
            to = from.AddMonths(1).AddDays(-1);
        }
        else
        {
            var offset = ((int)today.DayOfWeek + 6) % 7;
            from = today.AddDays(-offset);
            to = from.AddDays(6);
        }
        string F(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var (fs, ts) = (F(from), F(to));

        var bookings = await db.Bookings.Find(b => b.Status == BookingStatus.Confirmed
            && b.Date.CompareTo(fs) >= 0 && b.Date.CompareTo(ts) <= 0).ToListAsync(ct);
        var users = await db.Users.Find(u => u.Role != Roles.Admin).ToListAsync(ct);
        var byUser = users.Select(u =>
        {
            var mine = bookings.Where(b => b.UserId == u.Id).ToList();
            return new AnalyticsUserDto(u.Id, u.FullName, u.Email, mine.Count, mine.Sum(b => b.DurationHours),
                mine.Sum(b => b.TotalPrice));
        }).Where(x => x.Bookings > 0).OrderByDescending(x => x.Hours).ThenByDescending(x => x.Spent).ToList();

        return new AnalyticsDto(period == "month" ? "month" : "week", fs, ts, bookings.Count,
            bookings.Sum(b => b.DurationHours), bookings.Sum(b => b.TotalPrice), byUser);
    }
}
