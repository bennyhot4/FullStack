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

    public async Task<AuthTokens> IssueAsync(User user, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_o.AccessTokenMinutes);
        var jwt = new JwtSecurityToken(
            issuer: _o.JwtIssuer,
            audience: _o.JwtIssuer,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", user.Role),
            },
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
            ExpiresAt = now.AddDays(_o.RefreshTokenDays),
        }, cancellationToken: ct);

        return new AuthTokens(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, expires);
    }

    /// <summary>Consumes a refresh token (one-time use) and returns its user id, or null if invalid/expired.</summary>
    public async Task<string?> ConsumeRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var stored = await db.RefreshTokens.FindOneAndDeleteAsync(t => t.TokenHash == Hash(token), cancellationToken: ct);
        if (stored is null || stored.ExpiresAt <= clock.GetUtcNow().UtcDateTime) return null;
        return stored.UserId;
    }

    public Task RevokeAsync(string token, CancellationToken ct = default) =>
        db.RefreshTokens.DeleteOneAsync(t => t.TokenHash == Hash(token), ct);

    public Task RevokeAllForUserAsync(string userId, CancellationToken ct = default) =>
        db.RefreshTokens.DeleteManyAsync(t => t.UserId == userId, ct);

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
