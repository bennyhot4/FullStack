using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace HourOffice.Api.Services;

public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name, string? GivenName, string? FamilyName, string? Picture);

public interface IGoogleTokenValidator
{
    /// <summary>Validates a Google ID token (the "credential" from Google Identity Services). Returns null when invalid.</summary>
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct = default);
}

public sealed class GoogleTokenValidator(IOptions<AuthOptions> options, ILogger<GoogleTokenValidator> log) : IGoogleTokenValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var clientId = options.Value.GoogleClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            log.LogError("Auth:GoogleClientId is not configured; Google sign-in is unavailable.");
            return null;
        }
        try
        {
            var p = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });
            return new GoogleIdentity(p.Subject, p.Email, p.EmailVerified, p.Name, p.GivenName, p.FamilyName, p.Picture);
        }
        catch (InvalidJwtException ex)
        {
            log.LogWarning("Rejected Google token: {Reason}", ex.Message);
            return null;
        }
    }
}
