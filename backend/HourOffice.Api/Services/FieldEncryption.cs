using HourOffice.Api.Data;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Encryption;

namespace HourOffice.Api.Services;

/// <summary>Field-level encryption of personal data (plan 011, E3): MongoDB Client-Side Field Level Encryption in
/// explicit mode, which works on Atlas M0. Phones, client emails and notes are encrypted by the API before they reach
/// the database, with a data key that is itself wrapped by a master key in Google Cloud KMS ("local" key in
/// development and tests). A leaked dump or a stolen database password does not reveal them.</summary>
public sealed class FieldEncryptionOptions
{
    /// <summary>"off" (default), "encrypt" (write encrypted, read both) or "decrypt" (write plain, read both: the way back).</summary>
    public string Mode { get; set; } = "off";
    /// <summary>"local" (96-byte key from config, development and tests only) or "gcp" (Cloud KMS).</summary>
    public string KmsProvider { get; set; } = "local";
    /// <summary>Base64 of 96 random bytes. Never used in Production.</summary>
    public string LocalMasterKey { get; set; } = "";
    public GcpKmsOptions Gcp { get; set; } = new();
    /// <summary>Database of the key vault collection <c>encryptionKeys</c>; empty = the app database.</summary>
    public string KeyVaultDatabase { get; set; } = "";
    public string KeyAltName { get; set; } = "houroffice-pii";
}

public sealed class GcpKmsOptions
{
    public string ProjectId { get; set; } = "";
    public string Location { get; set; } = "europe-west1";
    public string KeyRing { get; set; } = "houroffice";
    public string KeyName { get; set; } = "mongo-cmk";
}

/// <summary>Process-wide encryption state used by <see cref="EncryptedSerializer{T}"/> (BSON serializers are global).
/// One API process uses one key vault and one mode.</summary>
public static class FieldEncryption
{
    public const string KeyVaultCollection = "encryptionKeys";
    private const string Algorithm = "AEAD_AES_256_CBC_HMAC_SHA_512-Random";
    private static ClientEncryption? _client;
    private static Guid _keyId;

    public static string Mode { get; private set; } = "off";
    public static bool Writing => Mode == "encrypt" && _client is not null;

    /// <summary>Called once at startup, before any document is read or written.</summary>
    public static async Task InitializeAsync(MongoContext db, FieldEncryptionOptions o, IHostEnvironment env, CancellationToken ct)
    {
        Mode = o.Mode is "encrypt" or "decrypt" ? o.Mode : "off";
        _client?.Dispose();
        _client = null;
        if (Mode == "off") return;

        var kms = new Dictionary<string, IReadOnlyDictionary<string, object>>();
        BsonDocument? masterKey = null;
        if (o.KmsProvider == "gcp")
        {
            // Empty: credentials come from the Cloud Run runtime service account (metadata server).
            kms["gcp"] = new Dictionary<string, object>();
            masterKey = new BsonDocument
            {
                ["projectId"] = o.Gcp.ProjectId, ["location"] = o.Gcp.Location,
                ["keyRing"] = o.Gcp.KeyRing, ["keyName"] = o.Gcp.KeyName,
            };
        }
        else
        {
            if (env.IsProduction()) throw new InvalidOperationException("FieldEncryption:KmsProvider must be gcp in Production.");
            var key = Convert.FromBase64String(o.LocalMasterKey);
            if (key.Length != 96) throw new InvalidOperationException("FieldEncryption:LocalMasterKey must be 96 bytes (base64).");
            kms["local"] = new Dictionary<string, object> { ["key"] = key };
        }

        var vaultDb = string.IsNullOrWhiteSpace(o.KeyVaultDatabase) ? db.Database.DatabaseNamespace.DatabaseName : o.KeyVaultDatabase;
        var vault = new CollectionNamespace(vaultDb, KeyVaultCollection);
        await db.Client.GetDatabase(vaultDb).GetCollection<BsonDocument>(KeyVaultCollection).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("keyAltNames"),
                new CreateIndexOptions<BsonDocument>
                {
                    Unique = true,
                    PartialFilterExpression = Builders<BsonDocument>.Filter.Exists("keyAltNames"),
                }), cancellationToken: ct);

        var client = new ClientEncryption(new ClientEncryptionOptions(db.Client, vault, kms));
        var existing = await client.GetKeyByAlternateKeyNameAsync(o.KeyAltName, ct);
        Guid keyId;
        if (existing is not null) keyId = existing["_id"].AsGuid;
        else
        {
            try
            {
                var opts = new DataKeyOptions(alternateKeyNames: new List<string> { o.KeyAltName }, masterKey: masterKey);
                keyId = await client.CreateDataKeyAsync(o.KmsProvider == "gcp" ? "gcp" : "local", opts, ct);
            }
            catch (Exception ex) when (MongoContext.IsDuplicateKey(ex) || ex.InnerException is { } inner && MongoContext.IsDuplicateKey(inner))
            {
                // Another instance created it at the same moment.
                keyId = (await client.GetKeyByAlternateKeyNameAsync(o.KeyAltName, ct))!["_id"].AsGuid;
            }
        }
        _keyId = keyId;
        _client = client;
    }

    internal static BsonBinaryData Encrypt(BsonValue value) =>
        (_client ?? throw new InvalidOperationException("Field encryption is not configured.")).Encrypt(value,
            new EncryptOptions(Algorithm, keyId: _keyId), CancellationToken.None);

    /// <summary>Fails closed: without the key (or with another key) the read throws instead of returning garbage.</summary>
    internal static BsonValue Decrypt(BsonBinaryData value) =>
        (_client ?? throw new InvalidOperationException("Encrypted data found but FieldEncryption:Mode is off."))
            .Decrypt(value, CancellationToken.None);
}

/// <summary>Stores a property encrypted (BinData subtype 6) when <see cref="FieldEncryption.Mode"/> is "encrypt";
/// always reads both plain and encrypted values, so a database can be migrated in either direction.</summary>
public sealed class EncryptedSerializer<T> : SerializerBase<T>
{
    private static IBsonSerializer<T> Inner => BsonSerializer.LookupSerializer<T>();

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
    {
        if (value is null) { context.Writer.WriteNull(); return; }
        if (!FieldEncryption.Writing) { Inner.Serialize(context, args, value); return; }
        var doc = new BsonDocument();
        using (var w = new BsonDocumentWriter(doc))
        {
            w.WriteStartDocument();
            w.WriteName("v");
            Inner.Serialize(BsonSerializationContext.CreateRoot(w), value);
            w.WriteEndDocument();
        }
        context.Writer.WriteBinaryData(FieldEncryption.Encrypt(doc["v"]));
    }

    public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        if (reader.CurrentBsonType != BsonType.Binary) return Inner.Deserialize(context, args);
        var bin = reader.ReadBinaryData();
        if (bin.SubType != BsonBinarySubType.Encrypted) throw new FormatException("Unexpected binary value in an encrypted field.");
        using var r = new BsonDocumentReader(new BsonDocument("v", FieldEncryption.Decrypt(bin)));
        r.ReadStartDocument();
        r.ReadName();
        var value = Inner.Deserialize(BsonDeserializationContext.CreateRoot(r));
        r.ReadEndDocument();
        return value;
    }
}
