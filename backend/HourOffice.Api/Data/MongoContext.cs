using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace HourOffice.Api.Data;

public sealed class MongoContext
{
    private static int _conventionsRegistered;

    public IMongoDatabase Database { get; }
    public IMongoCollection<User> Users => Database.GetCollection<User>("users");
    public IMongoCollection<Office> Offices => Database.GetCollection<Office>("offices");
    public IMongoCollection<Booking> Bookings => Database.GetCollection<Booking>("bookings");
    public IMongoCollection<SiteSetting> Settings => Database.GetCollection<SiteSetting>("siteSettings");
    public IMongoCollection<TermsDoc> Terms => Database.GetCollection<TermsDoc>("terms");
    public IMongoCollection<RefreshToken> RefreshTokens => Database.GetCollection<RefreshToken>("refreshTokens");
    public IMongoCollection<Client> Clients => Database.GetCollection<Client>("clients");

    public MongoContext(IOptions<MongoOptions> options)
    {
        RegisterConventions();
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ConnectionString))
            throw new InvalidOperationException("Mongo:ConnectionString is not configured (env var Mongo__ConnectionString).");
        Database = new MongoClient(o.ConnectionString).GetDatabase(o.Database);
    }

    private static void RegisterConventions()
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 1) return;
        ConventionRegistry.Register("houroffice", new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
        }, _ => true);
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
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

        await RefreshTokens.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.TokenHash), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
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
