using HourOffice.Api.Services;

namespace HourOffice.Api.Endpoints;

/// <summary>Office routes. The rules live in <see cref="OfficeService"/>, shared with the MCP tools.</summary>
public static class OfficeEndpoints
{
    public static void Map(WebApplication app)
    {
        var offices = app.MapGroup("/api/offices").RequireUser();

        offices.MapGet("", async (OfficeService svc, CancellationToken ct) => Results.Ok(await svc.ListAvailableAsync(ct)));

        offices.MapGet("/{id}", async (string id, HttpContext ctx, OfficeService svc, CancellationToken ct) =>
            (await svc.GetAsync(ctx.Me(), id, ct)).ToResult());

        offices.MapGet("/{id}/availability", async (string id, string? date, OfficeService svc, CancellationToken ct) =>
            (await svc.AvailabilityAsync(id, date, ct)).ToResult());

        var admin = app.MapGroup("/api/admin/offices").RequireAdmin();

        admin.MapGet("", async (OfficeService svc, CancellationToken ct) => Results.Ok(await svc.AdminListAsync(ct)));

        admin.MapPost("", async (OfficeRequest req, OfficeService svc, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(req, ct);
            return r.Error?.ToResult() ?? Results.Created($"/api/offices/{r.Value!.Id}", r.Value);
        });

        admin.MapPut("/{id}", async (string id, OfficeRequest req, OfficeService svc, CancellationToken ct) =>
            (await svc.UpdateAsync(id, req, ct)).ToResult());

        // Deleting an office cancels its future bookings; past bookings keep their history.
        admin.MapDelete("/{id}", async (string id, HttpContext ctx, OfficeService svc, CancellationToken ct) =>
            (await svc.DeleteAsync(ctx.Me(), id, ct))?.ToResult() ?? Results.NoContent());
    }
}
