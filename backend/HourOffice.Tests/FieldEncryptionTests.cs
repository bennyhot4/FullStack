using System.Net;
using System.Net.Http.Json;
using HourOffice.Api;
using HourOffice.Api.Data;
using HourOffice.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HourOffice.Tests;

/// <summary>Plan 011, E3: phones, client emails and notes are encrypted at rest, migrations work both ways, and
/// encrypted data never reads back as garbage.</summary>
[Collection("api")]
public sealed class FieldEncryptionTests(MongoFixture mongo)
{
    private static bool IsEncrypted(BsonValue v) => v is BsonBinaryData { SubType: BsonBinarySubType.Encrypted };

    private static IMongoCollection<BsonDocument> Raw(TestApp app, string name) =>
        app.Services.GetRequiredService<MongoContext>().Database.GetCollection<BsonDocument>(name);

    [Fact]
    public async Task Personal_fields_are_encrypted_at_rest_and_read_back_plain()
    {
        using var app = new TestApp(mongo);
        var admin = await app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var dana = await app.LoginAsync("dana@example.com", "Dana Levi");
        var client = await (await dana.PostAsJsonAsync("/api/clients", new { firstName = "Noa", lastName = "Cohen",
            phones = new[] { "050-7654321" }, emails = new[] { "noa@example.com" }, notes = "private note" })).Json();
        (await dana.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-10", startTime = "09:00",
            endTime = "10:00", notes = "booking note" })).EnsureSuccessStatusCode();

        var rawClient = await Raw(app, "clients").Find(new BsonDocument("_id", ObjectId.Parse(client.GetProperty("clientId").GetString()))).FirstAsync();
        Assert.True(IsEncrypted(rawClient["phones"]));
        Assert.True(IsEncrypted(rawClient["emails"]));
        Assert.True(IsEncrypted(rawClient["notes"]));
        Assert.Equal("Noa", rawClient["firstName"].AsString);
        Assert.DoesNotContain("7654321", rawClient.ToJson());
        var rawUser = await Raw(app, "users").Find(new BsonDocument("email", "dana@example.com")).FirstAsync();
        Assert.True(IsEncrypted(rawUser["phoneNumber"]));
        var rawBooking = await Raw(app, "bookings").Find(new BsonDocument()).FirstAsync();
        Assert.True(IsEncrypted(rawBooking["notes"]));
        // The admins' copy of Dana (plan 008) is encrypted too.
        var copy = await Raw(app, "clients").Find(new BsonDocument("sourceUserId", rawUser["_id"])).FirstAsync();
        Assert.True(IsEncrypted(copy["phones"]));

        var read = await (await dana.GetAsync("/api/clients")).Json();
        Assert.Equal("+972507654321", read[0].GetProperty("phones")[0].GetString());
        Assert.Equal("private note", read[0].GetProperty("notes").GetString());
        var mine = await (await dana.GetAsync("/api/bookings/mine")).Json();
        Assert.Equal("booking note", mine.GetProperty("upcoming")[0].GetProperty("notes").GetString());

        // Profile change updates the encrypted admin copy (read-modify-write, no positional update).
        (await dana.PutAsJsonAsync("/api/me", new { firstName = "Dana", lastName = "Levi", phone = "052-1111111" })).EnsureSuccessStatusCode();
        var adminClients = await (await admin.GetAsync("/api/clients")).Json();
        Assert.Contains(adminClients.EnumerateArray(), c => c.GetProperty("phones")[0].GetString() == "+972521111111");
    }

    [Fact]
    public async Task Migration_encrypts_existing_plain_documents_and_is_idempotent()
    {
        using var app = new TestApp(mongo);
        await app.AdminAsync();
        var userId = ObjectId.GenerateNewId();
        await Raw(app, "users").InsertOneAsync(new BsonDocument
        {
            ["_id"] = userId, ["email"] = "old@example.com", ["fullName"] = "Old User", ["phoneNumber"] = "+972501112222",
            ["role"] = "user", ["status"] = "active", ["createdAt"] = DateTime.UtcNow,
        });
        await Raw(app, "clients").InsertOneAsync(new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(), ["userId"] = userId, ["firstName"] = "Plain", ["lastName"] = "Client",
            ["phones"] = new BsonArray { "+972503334444" }, ["emails"] = new BsonArray { "plain@example.com" },
            ["notes"] = "old note", ["language"] = "he", ["createdAt"] = DateTime.UtcNow, ["updatedAt"] = DateTime.UtcNow,
        });

        var migrator = app.Services.GetRequiredService<FieldEncryptionMigrator>();
        Assert.True(await migrator.RunAsync(CancellationToken.None) >= 2);
        Assert.Equal(0, await migrator.RunAsync(CancellationToken.None));

        var user = await Raw(app, "users").Find(new BsonDocument("_id", userId)).FirstAsync();
        Assert.True(IsEncrypted(user["phoneNumber"]));
        var client = await Raw(app, "clients").Find(new BsonDocument("userId", userId)).FirstAsync();
        Assert.True(IsEncrypted(client["phones"]) && IsEncrypted(client["emails"]) && IsEncrypted(client["notes"]));
        var marker = await app.Services.GetRequiredService<MongoContext>().Settings
            .Find(s => s.Key == FieldEncryptionMigrator.MarkerKey).FirstAsync();
        Assert.Equal("true", marker.Value);
        var typed = await app.Services.GetRequiredService<MongoContext>().Clients.Find(c => c.UserId == userId.ToString()).FirstAsync();
        Assert.Equal("old note", typed.Notes);
    }

    [Fact]
    public async Task Decrypt_mode_restores_plain_values_and_off_mode_fails_closed()
    {
        var dbName = "t" + Guid.NewGuid().ToString("N");
        string clientId;
        using (var app = new TestApp(mongo, new() { ["Mongo:Database"] = dbName }))
        {
            var dana = await app.LoginAsync("dana@example.com");
            var c = await (await dana.PostAsJsonAsync("/api/clients", new { firstName = "Noa", lastName = "Cohen",
                phones = new[] { "050-7654321" }, notes = "n1" })).Json();
            clientId = c.GetProperty("clientId").GetString()!;
        }

        // Off with encrypted data present: reading fails instead of returning garbage.
        using (var off = new TestApp(mongo, new() { ["Mongo:Database"] = dbName, ["FieldEncryption:Mode"] = "off" }))
        {
            // Even sign-in fails: the user record holds an encrypted phone.
            var signIn = await off.CreateClient().PostAsJsonAsync("/api/auth/google", new { credential = "good:dana@example.com:Dana" });
            Assert.Equal(HttpStatusCode.InternalServerError, signIn.StatusCode);
        }

        using (var back = new TestApp(mongo, new() { ["Mongo:Database"] = dbName, ["FieldEncryption:Mode"] = "decrypt" }))
        {
            var migrator = back.Services.GetRequiredService<FieldEncryptionMigrator>();
            await migrator.RunAsync(CancellationToken.None);
            var raw = await Raw(back, "clients").Find(new BsonDocument("_id", ObjectId.Parse(clientId))).FirstAsync();
            Assert.Equal("+972507654321", raw["phones"][0].AsString);
            Assert.Equal("n1", raw["notes"].AsString);
        }
    }
}
