using System.Globalization;
using HourOffice.Api.Data;
using HourOffice.Api.Services;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

public static class AdminEndpoints
{
    public static void Map(WebApplication app)
    {
        var users = app.MapGroup("/api/admin/users").RequireAdmin();

        users.MapGet("", async (MongoContext db, CancellationToken ct) =>
        {
            var version = await TermsEndpoints.CurrentVersionAsync(db, ct);
            var list = await db.Users.Find(FilterDefinition<User>.Empty).SortByDescending(u => u.CreatedAt).ToListAsync(ct);
            return Results.Ok(list.Select(u => UserDto.From(u, version)));
        });

        // No email is sent: the record waits until the person signs in with Google using this address.
        users.MapPost("/invite", async (InviteUserRequest req, MongoContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email) || !email.Contains('@') || email.Length > 200) return Api.BadRequest("invalid_email");
            var role = req.Role ?? Roles.User;
            if (!Roles.IsValid(role)) return Api.BadRequest("invalid_role");
            var user = new User { Email = email, Role = role, Status = "invited", CreatedAt = clock.GetUtcNow().UtcDateTime };
            try
            {
                await db.Users.InsertOneAsync(user, cancellationToken: ct);
            }
            catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
            {
                return Api.Conflict("user_exists");
            }
            return Results.Created($"/api/admin/users/{user.Id}", UserDto.From(user, await TermsEndpoints.CurrentVersionAsync(db, ct)));
        });

        users.MapPut("/{id}", async (string id, AdminUpdateUserRequest req, HttpContext ctx, MongoContext db,
            AdminClientSync sync, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("user_not_found");
            var u = await db.Users.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
            if (u is null) return Api.NotFound("user_not_found");
            var wasAdmin = u.Role == Roles.Admin;
            if (req.Role is not null)
            {
                if (!Roles.IsValid(req.Role)) return Api.BadRequest("invalid_role");
                if (u.Id == ctx.Me().Id && req.Role != Roles.Admin) return Api.Conflict("cannot_demote_self");
                u.Role = req.Role;
            }
            if (req.FullName is not null)
            {
                var name = req.FullName.Trim();
                if (name.Length is 0 or > 120) return Api.BadRequest("name_required");
                u.FullName = name;
                var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                u.FirstName = parts.ElementAtOrDefault(0) ?? "";
                u.LastName = parts.ElementAtOrDefault(1) ?? "";
            }
            if (req.Email is not null)
            {
                var email = req.Email.Trim().ToLowerInvariant();
                if (!email.Contains('@')) return Api.BadRequest("invalid_email");
                u.Email = email;
            }
            if (req.PhoneNumber is not null)
            {
                if (req.PhoneNumber.Trim().Length == 0) u.PhoneNumber = null;
                else u.PhoneNumber = Phone.NormalizeIsraeli(req.PhoneNumber) ?? (string?)null;
                if (req.PhoneNumber.Trim().Length > 0 && u.PhoneNumber is null) return Api.BadRequest("invalid_phone");
            }
            try
            {
                await db.Users.ReplaceOneAsync(x => x.Id == id, u, cancellationToken: ct);
            }
            catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
            {
                return Api.Conflict("email_in_use");
            }
            if (req.FullName is not null)
                await db.Bookings.UpdateManyAsync(b => b.UserId == id, Builders<Booking>.Update.Set(b => b.BookerName, u.FullName), cancellationToken: ct);
            // A new admin gets every registered user as a client. An invited one gets them on first sign-in.
            if (!wasAdmin && u.Role == Roles.Admin) await sync.AddAllUsersToAdminAsync(u, ct);
            return Results.Ok(UserDto.From(u, await TermsEndpoints.CurrentVersionAsync(db, ct)));
        });

        users.MapDelete("/{id}", async (string id, HttpContext ctx, MongoContext db, TokenService tokens, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("user_not_found");
            if (id == ctx.Me().Id) return Api.Conflict("cannot_delete_self");
            var res = await db.Users.DeleteOneAsync(u => u.Id == id, ct);
            if (res.DeletedCount == 0) return Api.NotFound("user_not_found");
            await db.Bookings.DeleteManyAsync(b => b.UserId == id, ct);
            await db.Clients.DeleteManyAsync(c => c.UserId == id, ct);
            await db.DistributionLists.DeleteManyAsync(l => l.UserId == id, ct);
            // The admins' copies of this user, and those copies in the admins' distribution lists.
            var copies = await db.Clients.Find(c => c.SourceUserId == id).Project(c => c.Id).ToListAsync(ct);
            if (copies.Count > 0)
            {
                await db.DistributionLists.UpdateManyAsync(Builders<DistributionList>.Filter.AnyIn(l => l.ClientIds, copies),
                    Builders<DistributionList>.Update.PullAll(l => l.ClientIds, copies), cancellationToken: ct);
                await db.Clients.DeleteManyAsync(c => c.SourceUserId == id, ct);
            }
            await tokens.RevokeAllForUserAsync(id, ct);
            return Results.NoContent();
        });

        users.MapGet("/{id}/bookings", async (string id, MongoContext db, BookingRules rules, CancellationToken ct) =>
        {
            if (!Api.IsObjectId(id)) return Api.NotFound("user_not_found");
            var list = await db.Bookings.Find(b => b.UserId == id).ToListAsync(ct);
            return Results.Ok(list.OrderByDescending(b => b.Date, StringComparer.Ordinal)
                .ThenByDescending(b => b.StartTime, StringComparer.Ordinal).Select(b => BookingDto.From(b, rules)));
        });

        // Week starts on Monday. Only confirmed (incl. completed) bookings count; admins are excluded from the per-user list.
        app.MapGroup("/api/admin/analytics").RequireAdmin().MapGet("", async (string? period, MongoContext db,
            BookingRules rules, CancellationToken ct) =>
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
                return new
                {
                    userId = u.Id, fullName = u.FullName, email = u.Email,
                    bookings = mine.Count, hours = mine.Sum(b => b.DurationHours), spent = mine.Sum(b => b.TotalPrice),
                };
            }).Where(x => x.bookings > 0).OrderByDescending(x => x.hours).ThenByDescending(x => x.spent).ToList();

            return Results.Ok(new
            {
                period = period == "month" ? "month" : "week",
                from = fs, to = ts,
                totalBookings = bookings.Count,
                totalHours = bookings.Sum(b => b.DurationHours),
                revenue = bookings.Sum(b => b.TotalPrice),
                byUser,
            });
        });
    }
}
