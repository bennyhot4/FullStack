using HourOffice.Api.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

/// <summary>Brings existing documents to the current <see cref="FieldEncryption.Mode"/> (plan 011, E3): "encrypt"
/// encrypts plain fields, "decrypt" turns encrypted fields back into plain ones. Idempotent and resumable (each pass
/// selects only documents still in the old form), and safe next to live traffic: a document is replaced only if it
/// did not change since it was read. The marker <c>fields_encrypted</c> in siteSettings records the finished state.</summary>
public sealed class FieldEncryptionMigrator(MongoContext db, ILogger<FieldEncryptionMigrator> log)
{
    public const string MarkerKey = "fields_encrypted";
    private const int MaxPasses = 5;

    public async Task<long> RunAsync(CancellationToken ct)
    {
        if (FieldEncryption.Mode == "off") return 0;
        var encrypt = FieldEncryption.Mode == "encrypt";
        long changed = 0;
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var n = await MigrateAsync<User>("users", ["phoneNumber"], encrypt, ct)
                + await MigrateAsync<Client>("clients", ["phones", "emails", "notes"], encrypt, ct)
                + await MigrateAsync<Booking>("bookings", ["notes"], encrypt, ct);
            changed += n;
            if (n == 0) break;
        }
        await db.Settings.UpdateOneAsync(s => s.Key == MarkerKey,
            Builders<SiteSetting>.Update.Set(s => s.Value, encrypt ? "true" : "false")
                .SetOnInsert(s => s.Id, ObjectId.GenerateNewId().ToString()), new UpdateOptions { IsUpsert = true }, ct);
        log.LogInformation("Field encryption migration ({Mode}) changed {Count} documents", FieldEncryption.Mode, changed);
        return changed;
    }

    private async Task<long> MigrateAsync<T>(string collection, string[] fields, bool encrypt, CancellationToken ct)
    {
        var raw = db.Database.GetCollection<BsonDocument>(collection);
        var f = Builders<BsonDocument>.Filter;
        // Encrypting: any field still plain (string or array). Decrypting: any field still encrypted (binData).
        var filter = f.Or(fields.Select(name => encrypt
            ? f.Type(name, BsonType.String) | f.Type(name, BsonType.Array)
            : f.Type(name, BsonType.Binary)));
        long changed = 0;
        using var cursor = await raw.Find(filter).ToCursorAsync(ct);
        while (await cursor.MoveNextAsync(ct))
        {
            foreach (var original in cursor.Current)
            {
                var typed = BsonSerializer.Deserialize<T>(original);
                var next = typed.ToBsonDocument();
                // Optimistic concurrency: only if nobody changed the document since we read it.
                var same = f.And(original.Elements.Select(e => f.Eq(e.Name, e.Value)));
                var res = await raw.ReplaceOneAsync(same, next, cancellationToken: ct);
                changed += res.ModifiedCount;
            }
        }
        return changed;
    }
}
