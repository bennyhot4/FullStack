using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;

namespace HourOffice.Api.Services;

/// <summary>The profile and terms gates, shared by the REST filters and the MCP tools.</summary>
public static class UserGates
{
    public static ServiceError? Profile(User user) =>
        string.IsNullOrWhiteSpace(user.PhoneNumber) ? ServiceError.Forbidden("profile_required") : null;

    public static async Task<ServiceError?> TermsAsync(MongoContext db, User user, CancellationToken ct)
    {
        var version = await TermsEndpoints.CurrentVersionAsync(db, ct);
        return version > 0 && (user.TermsAcceptedVersion ?? 0) < version ? ServiceError.Forbidden("terms_required") : null;
    }

    /// <summary>Profile first, then terms (the order the booking endpoint has always used).</summary>
    public static async Task<ServiceError?> ProfileAndTermsAsync(MongoContext db, User user, CancellationToken ct) =>
        Profile(user) ?? await TermsAsync(db, user, ct);
}
