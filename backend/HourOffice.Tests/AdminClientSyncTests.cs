using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HourOffice.Api;
using HourOffice.Api.Data;
using HourOffice.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace HourOffice.Tests;

/// <summary>Plan 008: every registered user is copied into each admin's own client list.</summary>
[Collection("api")]
public sealed class AdminClientSyncTests(MongoFixture mongo) : IDisposable
{
    private readonly TestApp _app = new(mongo);
    public void Dispose() => _app.Dispose();

    private MongoContext Db => _app.Services.GetRequiredService<MongoContext>();

    private static async Task<string> MyIdAsync(HttpClient c) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user").GetProperty("id").GetString()!;

    private static async Task<JsonElement[]> ClientsAsync(HttpClient c) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/clients")).EnumerateArray().ToArray();

    private static JsonElement[] CopiesOf(JsonElement[] list, string userId) =>
        list.Where(c => c.TryGetProperty("sourceUserId", out var s) && s.GetString() == userId).ToArray();

    /// <summary>A second admin: invited as admin by the owner, then signs in (first sign-in activates them).</summary>
    private async Task<HttpClient> SecondAdminAsync(HttpClient owner, string email = "admin2@example.com")
    {
        (await owner.PostAsJsonAsync("/api/admin/users/invite", new { email, role = "admin" })).EnsureSuccessStatusCode();
        return await _app.LoginAsync(email, "Second Admin");
    }

    [Fact]
    public async Task Profile_completion_adds_user_to_each_admin()
    {
        var owner = await _app.AdminAsync();
        var admin2 = await SecondAdminAsync(owner);
        var user = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var userId = await MyIdAsync(user);

        foreach (var admin in new[] { owner, admin2 })
        {
            var copy = Assert.Single(CopiesOf(await ClientsAsync(admin), userId));
            Assert.Equal("Dana", copy.GetProperty("firstName").GetString());
            Assert.Equal("Tester", copy.GetProperty("lastName").GetString());
            Assert.Equal("+972501234567", copy.GetProperty("phones")[0].GetString());
            Assert.Equal("dana@example.com", copy.GetProperty("emails")[0].GetString());
            Assert.False(copy.GetProperty("whatsAppConsent").GetBoolean());
        }
        // A regular user's own list gets nothing.
        Assert.Empty(await ClientsAsync(user));
    }

    [Fact]
    public async Task User_without_profile_is_not_added()
    {
        var owner = await _app.AdminAsync();
        await _app.LoginAsync("half@example.com", ready: false);
        Assert.Empty(await ClientsAsync(owner));
    }

    [Fact]
    public async Task Second_profile_update_adds_nothing()
    {
        var owner = await _app.AdminAsync();
        var user = await _app.LoginAsync("dana@example.com", "Dana Levi");
        (await user.PutAsJsonAsync("/api/me", new { firstName = "Dana", lastName = "Tester", phone = "050-1234567" }))
            .EnsureSuccessStatusCode();
        Assert.Single(CopiesOf(await ClientsAsync(owner), await MyIdAsync(user)));
    }

    [Fact]
    public async Task Profile_change_updates_admin_copies()
    {
        var owner = await _app.AdminAsync();
        var admin2 = await SecondAdminAsync(owner);
        var user = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var userId = await MyIdAsync(user);
        var other = await _app.LoginAsync("other@example.com", "Other Person");

        // The owner adds a second phone, a note and English to their copy.
        var copy = Assert.Single(CopiesOf(await ClientsAsync(owner), userId));
        var copyId = copy.GetProperty("clientId").GetString();
        (await owner.PutAsJsonAsync($"/api/clients/{copyId}", new
        {
            firstName = "Dana", lastName = "Tester", phones = new[] { "050-1234567", "052-9999999" },
            emails = new[] { "dana@example.com" }, language = "en", whatsAppConsent = false, notes = "VIP",
        })).EnsureSuccessStatusCode();

        (await user.PutAsJsonAsync("/api/me", new { firstName = "Dina", lastName = "Cohen", phone = "054-7777777" }))
            .EnsureSuccessStatusCode();

        var updated = await owner.GetFromJsonAsync<JsonElement>($"/api/clients/{copyId}");
        Assert.Equal("Dina", updated.GetProperty("firstName").GetString());
        Assert.Equal("Cohen", updated.GetProperty("lastName").GetString());
        Assert.Equal(new[] { "+972547777777", "+972529999999" },
            updated.GetProperty("phones").EnumerateArray().Select(p => p.GetString()).ToArray());
        Assert.Equal("en", updated.GetProperty("language").GetString());
        Assert.Equal("VIP", updated.GetProperty("notes").GetString());

        var copy2 = Assert.Single(CopiesOf(await ClientsAsync(admin2), userId));
        Assert.Equal("Dina", copy2.GetProperty("firstName").GetString());

        // The other user's copy is untouched.
        var otherCopy = Assert.Single(CopiesOf(await ClientsAsync(owner), await MyIdAsync(other)));
        Assert.Equal("Other", otherCopy.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Deleted_copy_is_not_recreated_by_sync()
    {
        var owner = await _app.AdminAsync();
        var user = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var userId = await MyIdAsync(user);
        var copyId = Assert.Single(CopiesOf(await ClientsAsync(owner), userId)).GetProperty("clientId").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/clients/{copyId}")).StatusCode);

        (await user.PutAsJsonAsync("/api/me", new { firstName = "Dina", lastName = "Tester", phone = "050-1234567" }))
            .EnsureSuccessStatusCode();
        await _app.Services.GetRequiredService<AdminClientSync>().FillAllAdminsOnceAsync();

        Assert.Empty(CopiesOf(await ClientsAsync(owner), userId));
    }

    [Fact]
    public async Task Marketing_consent_default_false_and_synced()
    {
        var owner = await _app.AdminAsync();
        var user = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var userId = await MyIdAsync(user);
        var me = (await user.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user");
        Assert.False(me.GetProperty("marketingConsent").GetBoolean());

        var res = await user.PutAsJsonAsync("/api/me",
            new { firstName = "Dana", lastName = "Tester", phone = "050-1234567", marketingConsent = true });
        var body = await res.Json();
        Assert.True(body.GetProperty("marketingConsent").GetBoolean());
        Assert.Equal(_app.Clock.Now.UtcDateTime, body.GetProperty("marketingConsentAt").GetDateTime().ToUniversalTime());
        var copy = Assert.Single(CopiesOf(await ClientsAsync(owner), userId));
        Assert.True(copy.GetProperty("whatsAppConsent").GetBoolean());
        Assert.Equal(JsonValueKind.String, copy.GetProperty("whatsAppConsentAt").ValueKind);

        // Missing → unchanged.
        res = await user.PutAsJsonAsync("/api/me", new { firstName = "Dana", lastName = "Tester", phone = "050-1234567" });
        Assert.True((await res.Json()).GetProperty("marketingConsent").GetBoolean());

        res = await user.PutAsJsonAsync("/api/me",
            new { firstName = "Dana", lastName = "Tester", phone = "050-1234567", marketingConsent = false });
        body = await res.Json();
        Assert.False(body.GetProperty("marketingConsent").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("marketingConsentAt").ValueKind);
        copy = Assert.Single(CopiesOf(await ClientsAsync(owner), userId));
        Assert.False(copy.GetProperty("whatsAppConsent").GetBoolean());
        Assert.Equal(JsonValueKind.Null, copy.GetProperty("whatsAppConsentAt").ValueKind);
    }

    [Fact]
    public async Task Admin_is_not_added_to_own_list()
    {
        var owner = await _app.AdminAsync();
        var admin2 = await SecondAdminAsync(owner);
        var ownerId = await MyIdAsync(owner);
        var admin2Id = await MyIdAsync(admin2);

        Assert.Empty(CopiesOf(await ClientsAsync(owner), ownerId));
        Assert.Empty(CopiesOf(await ClientsAsync(admin2), admin2Id));
        Assert.Single(CopiesOf(await ClientsAsync(owner), admin2Id));
        Assert.Single(CopiesOf(await ClientsAsync(admin2), ownerId));
    }

    [Fact]
    public async Task Invited_admin_gets_nothing_until_first_sign_in()
    {
        var owner = await _app.AdminAsync();
        await _app.LoginAsync("dana@example.com", "Dana Levi");
        (await owner.PostAsJsonAsync("/api/admin/users/invite", new { email = "admin2@example.com", role = "admin" }))
            .EnsureSuccessStatusCode();
        var invitedId = (await Db.Users.Find(u => u.Email == "admin2@example.com").FirstAsync()).Id;
        Assert.Equal(0, await Db.Clients.CountDocumentsAsync(c => c.UserId == invitedId));

        var admin2 = await _app.LoginAsync("admin2@example.com", "Second Admin", ready: false);
        // Owner + Dana have completed profiles; the new admin has not (and is never their own client).
        Assert.Equal(2, (await Db.Clients.Find(c => c.UserId == invitedId).ToListAsync()).Count);
        Assert.Equal(HttpStatusCode.OK, (await admin2.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Admin_at_client_limit_is_skipped()
    {
        var owner = await _app.AdminAsync();
        var ownerId = await MyIdAsync(owner);
        var now = _app.Clock.Now.UtcDateTime;
        var have = await Db.Clients.CountDocumentsAsync(c => c.UserId == ownerId);
        await Db.Clients.InsertManyAsync(Enumerable.Range(0, Client.MaxPerUser - (int)have).Select(i => new Client
        {
            UserId = ownerId, FirstName = $"C{i}", Phones = new() { "+972500000000" }, CreatedAt = now, UpdatedAt = now,
        }));

        var user = await _app.LoginAsync("dana@example.com", "Dana Levi");
        Assert.Equal(Client.MaxPerUser, await Db.Clients.CountDocumentsAsync(c => c.UserId == ownerId));
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Promoted_user_gets_all_users()
    {
        var owner = await _app.AdminAsync();
        var dana = await _app.LoginAsync("dana@example.com", "Dana Levi");
        await _app.LoginAsync("half@example.com", ready: false);
        var promoted = await _app.LoginAsync("promoted@example.com", "Pro Moted");
        var promotedId = await MyIdAsync(promoted);
        var ownerBefore = (await ClientsAsync(owner)).Length;

        var res = await owner.PutAsJsonAsync($"/api/admin/users/{promotedId}", new { role = "admin" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await ClientsAsync(promoted);
        Assert.Equal(2, list.Length); // owner + Dana; not the half-registered user, not themself
        Assert.Single(CopiesOf(list, await MyIdAsync(dana)));
        Assert.Single(CopiesOf(list, await MyIdAsync(owner)));
        Assert.Equal(ownerBefore, (await ClientsAsync(owner)).Length);
    }

    [Fact]
    public async Task Admin_email_sign_in_gets_all_users()
    {
        // The owner email is in Auth:AdminEmails. Users who registered before the owner's first sign-in.
        var dana = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var owner = await _app.AdminAsync();
        Assert.Single(CopiesOf(await ClientsAsync(owner), await MyIdAsync(dana)));
    }

    [Fact]
    public async Task Becoming_admin_twice_adds_no_duplicates()
    {
        var owner = await _app.AdminAsync();
        await _app.LoginAsync("dana@example.com", "Dana Levi");
        var promoted = await _app.LoginAsync("promoted@example.com", "Pro Moted");
        var promotedId = await MyIdAsync(promoted);

        (await owner.PutAsJsonAsync($"/api/admin/users/{promotedId}", new { role = "admin" })).EnsureSuccessStatusCode();
        (await owner.PutAsJsonAsync($"/api/admin/users/{promotedId}", new { role = "user" })).EnsureSuccessStatusCode();
        (await owner.PutAsJsonAsync($"/api/admin/users/{promotedId}", new { role = "admin" })).EnsureSuccessStatusCode();

        Assert.Equal(2, await Db.Clients.CountDocumentsAsync(c => c.UserId == promotedId));
    }

    [Fact]
    public async Task Startup_fill_runs_once()
    {
        var owner = await _app.AdminAsync();
        var ownerId = await MyIdAsync(owner);
        var dana = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var danaId = await MyIdAsync(dana);
        var sync = _app.Services.GetRequiredService<AdminClientSync>();

        // The test app already ran the startup pass on an empty database, so the marker exists: no-op.
        await Db.Clients.DeleteManyAsync(c => c.UserId == ownerId && c.SourceUserId == danaId);
        await sync.FillAllAdminsOnceAsync();
        Assert.Equal(0, await Db.Clients.CountDocumentsAsync(c => c.UserId == ownerId && c.SourceUserId == danaId));

        // Without the marker (a database from before this feature), it fills the gap, twice is still one copy.
        await Db.Settings.DeleteOneAsync(s => s.Key == SettingKeys.AdminClientsFilled);
        await sync.FillAllAdminsOnceAsync();
        await Db.Settings.DeleteOneAsync(s => s.Key == SettingKeys.AdminClientsFilled);
        await sync.FillAllAdminsOnceAsync();
        Assert.Equal(1, await Db.Clients.CountDocumentsAsync(c => c.UserId == ownerId && c.SourceUserId == danaId));
    }

    [Fact]
    public async Task Deleting_user_removes_them_from_admin_clients_and_lists()
    {
        var owner = await _app.AdminAsync();
        var dana = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var danaId = await MyIdAsync(dana);
        var keep = await _app.LoginAsync("keep@example.com", "Keep Me");
        var keepId = await MyIdAsync(keep);
        var list = await ClientsAsync(owner);
        var danaCopy = CopiesOf(list, danaId)[0].GetProperty("clientId").GetString();
        var keepCopy = CopiesOf(list, keepId)[0].GetProperty("clientId").GetString();

        // Dana has her own client and list too.
        var own = await (await dana.PostAsJsonAsync("/api/clients", new
        {
            firstName = "Ruth", lastName = "", phones = new[] { "050-7654321" }, emails = Array.Empty<string>(),
            language = "he", whatsAppConsent = false, notes = "",
        })).Json();
        (await dana.PostAsJsonAsync("/api/distribution-lists",
            new { name = "Mine", clientIds = new[] { own.GetProperty("clientId").GetString() } })).EnsureSuccessStatusCode();

        var created = await (await owner.PostAsJsonAsync("/api/distribution-lists",
            new { name = "Renters", clientIds = new[] { danaCopy, keepCopy } })).Json();
        var listId = created.GetProperty("distributionListId").GetString();

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/admin/users/{danaId}")).StatusCode);

        Assert.Empty(CopiesOf(await ClientsAsync(owner), danaId));
        Assert.Single(CopiesOf(await ClientsAsync(owner), keepId));
        var after = await owner.GetFromJsonAsync<JsonElement>($"/api/distribution-lists/{listId}");
        Assert.Equal(new[] { keepCopy }, after.GetProperty("clientIds").EnumerateArray().Select(x => x.GetString()).ToArray());
        var stored = await Db.DistributionLists.Find(l => l.Id == listId).FirstAsync();
        Assert.Equal(new List<string> { keepCopy! }, stored.ClientIds);
        Assert.Equal(0, await Db.DistributionLists.CountDocumentsAsync(l => l.UserId == danaId));
        Assert.Equal(0, await Db.Clients.CountDocumentsAsync(c => c.UserId == danaId));
    }
}
