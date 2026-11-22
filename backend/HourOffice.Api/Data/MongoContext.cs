using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Encryption;

namespace HourOffice.Api.Data;

public sealed class MongoContext
{
    private static int _conventionsRegistered;

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public IMongoCollection<User> Users => Database.GetCollection<User>("users");
    public IMongoCollection<Office> Offices => Database.GetCollection<Office>("offices");
    public IMongoCollection<Booking> Bookings => Database.GetCollection<Booking>("bookings");
    public IMongoCollection<SiteSetting> Settings => Database.GetCollection<SiteSetting>("siteSettings");
    public IMongoCollection<TermsDoc> Terms => Database.GetCollection<TermsDoc>("terms");
    public IMongoCollection<RefreshToken> RefreshTokens => Database.GetCollection<RefreshToken>("refreshTokens");
    public IMongoCollection<Client> Clients => Database.GetCollection<Client>("clients");
    public IMongoCollection<DistributionList> DistributionLists => Database.GetCollection<DistributionList>("distributionLists");
    public IMongoCollection<McpConsentRequest> McpConsents => Database.GetCollection<McpConsentRequest>("mcpConsents");
    public IMongoCollection<McpAuditEntry> McpAudit => Database.GetCollection<McpAuditEntry>("mcpAuditLog");

    /// <summary>OpenIddict's own collections (plan 011). Their documents use OpenIddict's snake_case names.</summary>
    public const string OAuthApplications = "oauthApplications";
    public const string OAuthAuthorizations = "oauthAuthorizations";
    public const string OAuthScopes = "oauthScopes";
    public const string OAuthTokens = "oauthTokens";

    public MongoContext(IOptions<MongoOptions> options)
    {
        RegisterConventions();
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ConnectionString))
            throw new InvalidOperationException("Mongo:ConnectionString is not configured (env var Mongo__ConnectionString).");
        var settings = MongoClientSettings.FromConnectionString(o.ConnectionString);
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(o.Database);
    }

    private static void RegisterConventions()
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 1) return;
        ConventionRegistry.Register("houroffice", new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
        }, t => t.Namespace?.StartsWith("HourOffice", StringComparison.Ordinal) == true);
        // Only our own types: OpenIddict's documents keep the element names its store expects.
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
        // Field-level encryption (plan 011, E3) needs the driver's encryption extension (libmongocrypt).
        MongoClientSettings.Extensions.AddAutoEncryption();
    }

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Email), new CreateIndexOptions { Unique = true }), cancellationToken: ct);

        await Offices.Indexes.CreateOneAsync(new CreateIndexModel<Office>(
            Builders<Office>.IndexKeys.Ascending(o => o.IsAvailable).Descending(o => o.CreatedAt)), cancellationToken: ct);

        // The double-booking guard: no two confirmed bookings may share an office + date + hour slot.
        await Bookings.Indexes.CreateOneAsync(new CreateIndexModel<Booking>(
            Builders<Booking>.IndexKeys.Ascending(b => b.OfficeId).Ascending(b => b.Date).Ascending(b => b.Slots),
            new CreateIndexOptions<Booking>
            {
                Name = "uniq_confirmed_slot",
                Unique = true,
                PartialFilterExpression = Builders<Booking>.Filter.Eq(b => b.Status, BookingStatus.Confirmed),
            }), cancellationToken: ct);
        await Bookings.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Booking>(Builders<Booking>.IndexKeys.Ascending(b => b.UserId).Descending(b => b.Date)),
            new CreateIndexModel<Booking>(Builders<Booking>.IndexKeys.Ascending(b => b.Date).Ascending(b => b.Status)),
        }, ct);

        await Settings.Indexes.CreateOneAsync(new CreateIndexModel<SiteSetting>(
            Builders<SiteSetting>.IndexKeys.Ascending(s => s.Key), new CreateIndexOptions { Unique = true }), cancellationToken: ct);

        await Terms.Indexes.CreateOneAsync(new CreateIndexModel<TermsDoc>(
            Builders<TermsDoc>.IndexKeys.Descending(t => t.Version), new CreateIndexOptions { Unique = true }), cancellationToken: ct);

        // A user's client list, sorted by name. No uniqueness on phones/emails: duplicates are allowed.
        await Clients.Indexes.CreateOneAsync(new CreateIndexModel<Client>(
            Builders<Client>.IndexKeys.Ascending(c => c.UserId).Ascending(c => c.LastName).Ascending(c => c.FirstName)),
            cancellationToken: ct);
        // One copy of a registered user per admin (see AdminClientSync). Only clients that have a sourceUserId.
        await Clients.Indexes.CreateOneAsync(new CreateIndexModel<Client>(
            Builders<Client>.IndexKeys.Ascending(c => c.UserId).Ascending(c => c.SourceUserId),
            new CreateIndexOptions<Client>
            {
                Name = "uniq_owner_source_user",
                Unique = true,
                PartialFilterExpression = Builders<Client>.Filter.Type(c => c.SourceUserId, BsonType.ObjectId),
            }), cancellationToken: ct);

        await DistributionLists.Indexes.CreateOneAsync(new CreateIndexModel<DistributionList>(
            Builders<DistributionList>.IndexKeys.Ascending(l => l.UserId).Ascending(l => l.Name)), cancellationToken: ct);

        // MCP (plan 011): pending consent requests and the audit log clean themselves up.
        await McpConsents.Indexes.CreateOneAsync(new CreateIndexModel<McpConsentRequest>(
            Builders<McpConsentRequest>.IndexKeys.Ascending(c => c.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
            cancellationToken: ct);
        await McpAudit.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<McpAuditEntry>(Builders<McpAuditEntry>.IndexKeys.Ascending(a => a.UserId).Descending(a => a.At)),
            new CreateIndexModel<McpAuditEntry>(Builders<McpAuditEntry>.IndexKeys.Ascending(a => a.ExpiresAt),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
        }, ct);
        await EnsureOAuthIndexesAsync(ct);

        await RefreshTokens.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.TokenHash), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
        }, ct);
    }

    /// <summary>OpenIddict's MongoDB store does not create indexes itself. Field names are OpenIddict's.</summary>
    private async Task EnsureOAuthIndexesAsync(CancellationToken ct)
    {
        var keys = Builders<BsonDocument>.IndexKeys;
        await Database.GetCollection<BsonDocument>(OAuthApplications).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(keys.Ascending("client_id"), new CreateIndexOptions { Unique = true }),
            cancellationToken: ct);
        await Database.GetCollection<BsonDocument>(OAuthScopes).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(keys.Ascending("name"), new CreateIndexOptions { Unique = true }),
            cancellationToken: ct);
        await Database.GetCollection<BsonDocument>(OAuthAuthorizations).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(keys.Ascending("subject").Ascending("application_id").Ascending("status")),
            cancellationToken: ct);
        await Database.GetCollection<BsonDocument>(OAuthTokens).Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BsonDocument>(keys.Ascending("authorization_id")),
            new CreateIndexModel<BsonDocument>(keys.Ascending("subject").Ascending("application_id").Ascending("status")),
            new CreateIndexModel<BsonDocument>(keys.Ascending("reference_id"), new CreateIndexOptions<BsonDocument>
            {
                Unique = true,
                PartialFilterExpression = Builders<BsonDocument>.Filter.Type("reference_id", BsonType.String),
            }),
            // Expired tokens are deleted by MongoDB.
            new CreateIndexModel<BsonDocument>(keys.Ascending("expiration_date"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
        }, ct);
    }

    public static bool IsDuplicateKey(Exception ex) => ex switch
    {
        MongoWriteException w => w.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoBulkWriteException b => b.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey),
        MongoCommandException c => c.Code == 11000,
        _ => false,
    };
}
