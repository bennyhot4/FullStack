using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HourOffice.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);

/// <summary>A consumed refresh token: whose it was and when that session signed in with Google.</summary>
public sealed record RefreshedSession(string UserId, DateTime? AuthTime);

/// <summary>Issues short-lived JWT access tokens and rotating opaque refresh tokens (stored hashed).</summary>
public sealed class TokenService(IOptions<AuthOptions> options, MongoContext db, TimeProvider clock)
{
    private readonly AuthOptions _o = options.Value;

    public static SymmetricSecurityKey SigningKey(AuthOptions o)
    {
        if (string.IsNullOrEmpty(o.JwtSigningKey) || o.JwtSigningKey.Length < 32)
            throw new InvalidOperationException("Auth:JwtSigningKey must be at least 32 characters (env var Auth__JwtSigningKey).");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.JwtSigningKey));
    }

    /// <summary>The claim with the time this session signed in (seconds since 1970). A refresh keeps the original
    /// value, so "signed in recently" means this session, not some other sign-in of the account (plan 011, S1).</summary>
    public const string AuthTimeClaim = "auth_time";

    public async Task<AuthTokens> IssueAsync(User user, DateTime? authTime, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role),
        };
        if (authTime is { } at)
            claims.Add(new Claim(AuthTimeClaim, new DateTimeOffset(at, TimeSpan.Zero).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64));
        var expires = now.AddMinutes(_o.AccessTokenMinutes);
        var jwt = new JwtSecurityToken(
            issuer: _o.JwtIssuer,
            audience: _o.JwtIssuer,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(SigningKey(_o), SecurityAlgorithms.HmacSha256));

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        await db.RefreshTokens.InsertOneAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refresh),
            CreatedAt = now,
            AuthTime = authTime,
            ExpiresAt = now.AddDays(_o.RefreshTokenDays),
        }, cancellationToken: ct);

        return new AuthTokens(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, expires);
    }

    /// <summary>Consumes a refresh token (one-time use) and returns its session, or null if invalid/expired.</summary>
    public async Task<RefreshedSession?> ConsumeRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var stored = await db.RefreshTokens.FindOneAndDeleteAsync(t => t.TokenHash == Hash(token), cancellationToken: ct);
        if (stored is null || stored.ExpiresAt <= clock.GetUtcNow().UtcDateTime) return null;
        return new RefreshedSession(stored.UserId, stored.AuthTime);
    }

    /// <summary>When the session behind this request signed in, from its access token; null if unknown.</summary>
    public static DateTime? AuthTimeOf(ClaimsPrincipal p) =>
        long.TryParse(p.FindFirstValue(AuthTimeClaim), out var s) ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime : null;

    public Task RevokeAsync(string token, CancellationToken ct = default) =>
        db.RefreshTokens.DeleteOneAsync(t => t.TokenHash == Hash(token), ct);

    public Task RevokeAllForUserAsync(string userId, CancellationToken ct = default) =>
        db.RefreshTokens.DeleteManyAsync(t => t.UserId == userId, ct);

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
