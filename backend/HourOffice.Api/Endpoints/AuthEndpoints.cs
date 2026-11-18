using HourOffice.Api.Data;
using HourOffice.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HourOffice.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth");

        auth.MapPost("/google", async (GoogleLoginRequest req, IGoogleTokenValidator google, MongoContext db,
            TokenService tokens, IOptions<AuthOptions> opts, TimeProvider clock, AdminClientSync sync, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Credential)) return Api.BadRequest("credential_required");
            var id = await google.ValidateAsync(req.Credential, ct);
            if (id is null) return Api.Error(401, "invalid_google_token");
            if (!id.EmailVerified) return Api.Error(401, "email_not_verified");
            return await SignInAsync(db, tokens, opts.Value, clock, sync, id.Email, id.Subject,
                id.GivenName, id.FamilyName, id.Name, id.Picture, ct);
        });

        // Email-only sign-in for local development and automated tests. Never mapped in Production.
        if (!app.Environment.IsProduction() && app.Services.GetRequiredService<IOptions<AuthOptions>>().Value.DevLoginEnabled)
        {
            auth.MapPost("/dev-login", async (DevLoginRequest req, MongoContext db, TokenService tokens,
                IOptions<AuthOptions> opts, TimeProvider clock, AdminClientSync sync, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@')) return Api.BadRequest("invalid_email");
                var name = string.IsNullOrWhiteSpace(req.Name) ? req.Email.Split('@')[0] : req.Name.Trim();
                var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                return await SignInAsync(db, tokens, opts.Value, clock, sync, req.Email, null,
                    parts.ElementAtOrDefault(0), parts.ElementAtOrDefault(1), name, null, ct);
            });
        }

        auth.MapPost("/refresh", async (RefreshRequest req, TokenService tokens, MongoContext db, CancellationToken ct) =>
        {
            var userId = await tokens.ConsumeRefreshTokenAsync(req.RefreshToken ?? "", ct);
            if (userId is null) return Api.Error(401, "invalid_refresh_token");
            var user = await db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(ct);
            if (user is null) return Api.Error(401, "invalid_refresh_token");
            var t = await tokens.IssueAsync(user, ct);
            var version = await TermsEndpoints.CurrentVersionAsync(db, ct);
            return Results.Ok(new AuthResponse(t.AccessToken, t.RefreshToken, t.AccessTokenExpiresAt, UserDto.From(user, version)));
        });

        auth.MapPost("/logout", async (RefreshRequest req, TokenService tokens, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(req.RefreshToken)) await tokens.RevokeAsync(req.RefreshToken, ct);
            return Results.NoContent();
        });

        var me = app.MapGroup("/api/me").RequireUser();

        me.MapGet("", async (HttpContext ctx, MongoContext db, CancellationToken ct) =>
        {
            var user = ctx.Me();
            var version = await TermsEndpoints.CurrentVersionAsync(db, ct);
            var dto = UserDto.From(user, version);
            return Results.Ok(new
            {
                user = dto,
                needsProfile = string.IsNullOrWhiteSpace(user.PhoneNumber),
                needsTerms = version > 0 && !dto.TermsAccepted,
                currentTermsVersion = version,
            });
        });

        // The first completion adds the user to every admin's client list; later changes update those copies.
        me.MapPut("", async (UpdateProfileRequest req, HttpContext ctx, MongoContext db, TimeProvider clock,
            AdminClientSync sync, CancellationToken ct) =>
        {
            var first = req.FirstName?.Trim() ?? "";
            var last = req.LastName?.Trim() ?? "";
            if (first.Length is 0 or > 60 || last.Length is 0 or > 60) return Api.BadRequest("name_required");
            var phone = Phone.NormalizeIsraeli(req.Phone);
            if (phone is null) return Api.BadRequest("invalid_phone", "Please enter a valid phone number");
            var user = ctx.Me();
            var hadProfile = !string.IsNullOrWhiteSpace(user.PhoneNumber);
            user.FirstName = first;
            user.LastName = last;
            user.FullName = $"{first} {last}";
            user.PhoneNumber = phone;
            if (req.MarketingConsent is { } consent)
            {
                // The server records when consent was given; turning it off clears the time.
                if (consent && !user.MarketingConsent) user.MarketingConsentAt = clock.GetUtcNow().UtcDateTime;
                if (!consent) user.MarketingConsentAt = null;
                user.MarketingConsent = consent;
            }
            await db.Users.ReplaceOneAsync(u => u.Id == user.Id, user, cancellationToken: ct);
            if (hadProfile) await sync.UpdateUserInAdminsAsync(user, ct);
            else await sync.AddUserToAdminsAsync(user, ct);
            return Results.Ok(UserDto.From(user, await TermsEndpoints.CurrentVersionAsync(db, ct)));
        });

        me.MapPost("/accept-terms", async (AcceptTermsRequest req, HttpContext ctx, MongoContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var version = await TermsEndpoints.CurrentVersionAsync(db, ct);
            if (version == 0 || req.Version != version) return Api.Conflict("terms_outdated", "The terms changed. Please review the latest version.");
            var user = ctx.Me();
            var now = clock.GetUtcNow().UtcDateTime;
            await db.Users.UpdateOneAsync(u => u.Id == user.Id, Builders<User>.Update
                .Set(u => u.TermsAcceptedVersion, version)
                .Set(u => u.TermsAcceptedAt, now), cancellationToken: ct);
            user.TermsAcceptedVersion = version;
            user.TermsAcceptedAt = now;
            return Results.Ok(UserDto.From(user, version));
        });
    }

    private static async Task<IResult> SignInAsync(MongoContext db, TokenService tokens, AuthOptions o, TimeProvider clock,
        AdminClientSync sync, string email, string? googleSub, string? first, string? last, string? fullName, string? picture, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        var now = clock.GetUtcNow().UtcDateTime;
        var isAdminEmail = o.AdminEmails.Any(a => string.Equals(a.Trim(), email, StringComparison.OrdinalIgnoreCase));
        var user = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync(ct);
        var wasActiveAdmin = user is { Role: Roles.Admin, Status: "active" };

        if (user is null)
        {
            if (!o.AllowOpenSignup && !isAdminEmail) return Api.Forbidden("user_not_registered");
            user = new User
            {
                Email = email,
                Role = isAdminEmail ? Roles.Admin : Roles.User,
                CreatedAt = now,
            };
        }

        user.GoogleSub ??= googleSub;
        if (string.IsNullOrWhiteSpace(user.FirstName)) user.FirstName = first ?? "";
        if (string.IsNullOrWhiteSpace(user.LastName)) user.LastName = last ?? "";
        if (string.IsNullOrWhiteSpace(user.FullName))
            user.FullName = fullName ?? $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(picture)) user.PictureUrl = picture;
        if (isAdminEmail) user.Role = Roles.Admin;
        user.Status = "active";
        user.LastLoginAt = now;

        try
        {
            await db.Users.ReplaceOneAsync(u => u.Id == user.Id, user, new ReplaceOptions { IsUpsert = true }, ct);
        }
        catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
        {
            // Two first sign-ins raced; the other one created the record.
            user = await db.Users.Find(u => u.Email == email).FirstAsync(ct);
        }

        // Promoted through Auth:AdminEmails, a new admin, or an invited admin's first sign-in.
        if (!wasActiveAdmin && user.Role == Roles.Admin) await sync.AddAllUsersToAdminAsync(user, ct);

        var t = await tokens.IssueAsync(user, ct);
        var version = await TermsEndpoints.CurrentVersionAsync(db, ct);
        return Results.Ok(new AuthResponse(t.AccessToken, t.RefreshToken, t.AccessTokenExpiresAt, UserDto.From(user, version)));
    }
}
