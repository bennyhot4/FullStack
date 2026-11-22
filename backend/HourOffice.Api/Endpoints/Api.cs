using System.Security.Claims;
using HourOffice.Api.Data;
using HourOffice.Api.Services;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

/// <summary>Shared helpers for endpoint handlers.</summary>
public static class Api
{
    public static IResult Error(int status, string code, string? message = null) =>
        Results.Json(new { code, message = message ?? code }, statusCode: status);

    public static IResult BadRequest(string code, string? message = null) => Error(400, code, message);
    public static IResult NotFound(string code = "not_found") => Error(404, code);
    public static IResult Conflict(string code, string? message = null) => Error(409, code, message);
    public static IResult Forbidden(string code = "forbidden") => Error(403, code);

    public static IResult ToResult(this ServiceError e) => Error(e.Status, e.Code, e.Message);

    /// <summary>200 with the value, or the error as <c>{ code, message }</c>.</summary>
    public static IResult ToResult<T>(this Outcome<T> o) => o.Error?.ToResult() ?? Results.Ok(o.Value);

    public static string? UserId(this ClaimsPrincipal p) =>
        p.FindFirstValue(ClaimTypes.NameIdentifier) ?? p.FindFirstValue("sub");

    /// <summary>Loads the signed-in user from the database (roles are read fresh, never trusted from the token).</summary>
    public static async Task<User?> CurrentUserAsync(this HttpContext ctx, MongoContext db)
    {
        if (ctx.Items.TryGetValue("currentUser", out var cached)) return cached as User;
        var id = ctx.User.UserId();
        User? user = null;
        if (MongoDB.Bson.ObjectId.TryParse(id, out _))
            user = await db.Users.Find(u => u.Id == id).FirstOrDefaultAsync(ctx.RequestAborted);
        ctx.Items["currentUser"] = user;
        return user;
    }

    public static bool IsObjectId(string? id) => MongoDB.Bson.ObjectId.TryParse(id, out _);

    /// <summary>Endpoint filter: 401 when the user record is gone, 403 when not an admin.</summary>
    public static RouteGroupBuilder RequireAdmin(this RouteGroupBuilder g)
    {
        g.RequireAuthorization();
        g.AddEndpointFilter(async (ctx, next) =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<MongoContext>();
            var user = await ctx.HttpContext.CurrentUserAsync(db);
            if (user is null) return Error(401, "unauthorized");
            if (user.Role != Roles.Admin) return Forbidden("admin_required");
            return await next(ctx);
        });
        return g;
    }

    /// <summary>Endpoint filter: 401 when the token's user no longer exists.</summary>
    public static RouteGroupBuilder RequireUser(this RouteGroupBuilder g)
    {
        g.RequireAuthorization();
        g.AddEndpointFilter(async (ctx, next) =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<MongoContext>();
            return await ctx.HttpContext.CurrentUserAsync(db) is null ? Error(401, "unauthorized") : await next(ctx);
        });
        return g;
    }

    /// <summary>Endpoint filter: 403 terms_required when the signed-in user has not accepted the current
    /// terms version. Add it after <see cref="RequireUser"/> (group filters run first).</summary>
    public static TBuilder RequireTerms<TBuilder>(this TBuilder b) where TBuilder : IEndpointConventionBuilder
    {
        b.AddEndpointFilter(async (ctx, next) =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<MongoContext>();
            var user = await ctx.HttpContext.CurrentUserAsync(db);
            if (user is null) return Error(401, "unauthorized");
            var gate = await UserGates.TermsAsync(db, user, ctx.HttpContext.RequestAborted);
            return gate is not null ? gate.ToResult() : await next(ctx);
        });
        return b;
    }

    public static User Me(this HttpContext ctx) => (User)ctx.Items["currentUser"]!;
}
