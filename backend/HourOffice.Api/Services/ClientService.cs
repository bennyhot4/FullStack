using HourOffice.Api.Data;
using HourOffice.Api.Endpoints;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

/// <summary>Client reads shared by <see cref="ClientEndpoints"/>, the booking rules and the MCP tools. Every query
/// filters by the owner's id inside the MongoDB filter.</summary>
public sealed class ClientService(MongoContext db)
{
    /// <summary>The owner's clients sorted by name; with <paramref name="search"/> only names that contain it.</summary>
    public async Task<List<Client>> ListAsync(string ownerId, string? search, CancellationToken ct)
    {
        var list = await db.Clients.Find(c => c.UserId == ownerId)
            .SortBy(c => c.LastName).ThenBy(c => c.FirstName).Limit(Client.MaxPerUser).ToListAsync(ct);
        var s = search?.Trim();
        return string.IsNullOrEmpty(s) ? list
            : list.Where(c => ClientDto.FullName(c).Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>The client only if it belongs to <paramref name="userId"/>; null otherwise.</summary>
    internal static async Task<Client?> FindOwnAsync(MongoContext db, string? id, string userId, CancellationToken ct) =>
        Endpoints.Api.IsObjectId(id) ? await db.Clients.Find(c => c.Id == id && c.UserId == userId).FirstOrDefaultAsync(ct) : null;
}
