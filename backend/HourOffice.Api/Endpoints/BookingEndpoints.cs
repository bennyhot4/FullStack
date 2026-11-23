using HourOffice.Api.Services;

namespace HourOffice.Api.Endpoints;

/// <summary>Booking routes. The rules live in <see cref="BookingService"/>, shared with the MCP tools.</summary>
public static class BookingEndpoints
{
    public static void Map(WebApplication app)
    {
        var bookings = app.MapGroup("/api/bookings").RequireUser();

        // Profile and terms gates run inside the service (profile first), like before.
        bookings.MapPost("", async (CreateBookingRequest req, HttpContext ctx, BookingService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(ctx.Me(), req, ct);
            return r.Error?.ToResult() ?? Results.Created($"/api/bookings/{r.Value!.Id}", r.Value);
        });

        bookings.MapGet("/mine", async (HttpContext ctx, BookingService svc, CancellationToken ct) =>
            Results.Ok(await svc.MineAsync(ctx.Me().Id, null, ct)));

        // The owner sets, changes or removes (clientId: null) the booking's client. Cancelled bookings too:
        // it is only a label. Someone else's booking or client → 404.
        bookings.MapPut("/{id}/client", async (string id, SetBookingClientRequest req, HttpContext ctx, BookingService svc,
            CancellationToken ct) => (await svc.SetClientAsync(ctx.Me(), id, req.ClientId, ct)).ToResult());

        bookings.MapPost("/{id}/cancel", async (string id, HttpContext ctx, BookingService svc, CancellationToken ct) =>
            (await svc.CancelAsync(ctx.Me(), id, ct)).ToResult());

        var admin = app.MapGroup("/api/admin/bookings").RequireAdmin();

        admin.MapGet("", async (int? limit, string? userId, BookingService svc, CancellationToken ct) =>
            Results.Ok(await svc.AdminListAsync(limit, userId, null, null, null, ct)));

        admin.MapPut("/{id}", async (string id, AdminUpdateBookingRequest req, BookingService svc, CancellationToken ct) =>
            (await svc.AdminUpdateAsync(id, req, ct)).ToResult());

        admin.MapPost("/{id}/cancel", async (string id, HttpContext ctx, BookingService svc, CancellationToken ct) =>
            (await svc.AdminCancelAsync(ctx.Me(), id, ct)).ToResult());

        // Creates one booking per matching weekday in [fromDate, toDate]; hours that are already taken are skipped.
        admin.MapPost("/recurring", async (RecurringBookingRequest req, HttpContext ctx, BookingService svc,
            CancellationToken ct) =>
            (await svc.RecurringAsync(ctx.Me(), req, svc.Rules.Options.RecurringMaxDays, ct)).ToResult());
    }
}
