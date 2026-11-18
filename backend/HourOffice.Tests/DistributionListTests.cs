using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HourOffice.Api;
using HourOffice.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace HourOffice.Tests;

/// <summary>Plan 008: private distribution lists of the owner's own clients.</summary>
[Collection("api")]
public sealed class DistributionListTests(MongoFixture mongo) : IDisposable
{
    private readonly TestApp _app = new(mongo);
    public void Dispose() => _app.Dispose();

    private static async Task<string> AddClientAsync(HttpClient c, string first, string last = "Cohen")
    {
        var res = await c.PostAsJsonAsync("/api/clients", new
        {
            firstName = first, lastName = last, phones = new[] { "050-7654321" }, emails = Array.Empty<string>(),
            language = "he", whatsAppConsent = false, notes = "",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Json()).GetProperty("clientId").GetString()!;
    }

    private static async Task<string> CreateListAsync(HttpClient c, string name, params string[] clientIds)
    {
        var res = await c.PostAsJsonAsync("/api/distribution-lists", new { name, description = "", clientIds });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Json()).GetProperty("distributionListId").GetString()!;
    }

    private static string[] Ids(JsonElement arr) => arr.EnumerateArray().Select(x => x.GetString()!).ToArray();

    [Fact]
    public async Task Distribution_list_crud()
    {
        var user = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var ruth = await AddClientAsync(user, "Ruth", "Cohen");
        var avi = await AddClientAsync(user, "Avi", "Amar");

        var res = await user.PostAsJsonAsync("/api/distribution-lists",
            new { name = "  Monday group ", description = " weekly ", clientIds = new[] { ruth, avi } });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Json();
        var id = created.GetProperty("distributionListId").GetString();
        Assert.Equal("Monday group", created.GetProperty("name").GetString());
        Assert.Equal("weekly", created.GetProperty("description").GetString());
        Assert.False(created.TryGetProperty("userId", out _));

        var all = (await user.GetFromJsonAsync<JsonElement>("/api/distribution-lists")).EnumerateArray().ToArray();
        Assert.Equal(2, Assert.Single(all).GetProperty("clientCount").GetInt32());

        var got = await user.GetFromJsonAsync<JsonElement>($"/api/distribution-lists/{id}");
        var members = got.GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Avi Amar", "Ruth Cohen" }, members.Select(m => m.GetProperty("fullName").GetString()).ToArray());
        Assert.Equal("+972507654321", members[0].GetProperty("phone").GetString());

        res = await user.PutAsJsonAsync($"/api/distribution-lists/{id}", new { name = "Renamed", clientIds = new[] { ruth } });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await res.Json();
        Assert.Equal("Renamed", updated.GetProperty("name").GetString());
        Assert.Equal(new[] { ruth }, Ids(updated.GetProperty("clientIds")));

        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/distribution-lists/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/distribution-lists/{id}")).StatusCode);
        // The clients stay.
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/clients/{ruth}")).StatusCode);
    }

    [Fact]
    public async Task Other_user_cannot_read_update_or_delete_list()
    {
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var admin = await _app.AdminAsync();
        var ruth = await AddClientAsync(a, "Ruth");
        var id = await CreateListAsync(a, "Mine", ruth);

        foreach (var other in new[] { b, admin })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/distribution-lists/{id}")).StatusCode);
            var put = await other.PutAsJsonAsync($"/api/distribution-lists/{id}", new { name = "Hacked", clientIds = Array.Empty<string>() });
            Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
            Assert.Equal("distribution_list_not_found", await put.Code());
            Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/distribution-lists/{id}")).StatusCode);
            Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/distribution-lists")).EnumerateArray());
        }

        var still = await a.GetFromJsonAsync<JsonElement>($"/api/distribution-lists/{id}");
        Assert.Equal("Mine", still.GetProperty("name").GetString());
        Assert.Equal(new[] { ruth }, Ids(still.GetProperty("clientIds")));
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync("/api/distribution-lists/not-an-id")).StatusCode);
    }

    [Fact]
    public async Task List_with_foreign_client_rejected()
    {
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var mine = await AddClientAsync(a, "Ruth");
        var theirs = await AddClientAsync(b, "Avi");

        var res = await a.PostAsJsonAsync("/api/distribution-lists", new { name = "Mixed", clientIds = new[] { mine, theirs } });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client_not_found", await res.Code());
        res = await a.PostAsJsonAsync("/api/distribution-lists", new { name = "Bad", clientIds = new[] { "nope" } });
        Assert.Equal("client_not_found", await res.Code());
        Assert.Empty((await a.GetFromJsonAsync<JsonElement>("/api/distribution-lists")).EnumerateArray());

        // Updating with a foreign id leaves the list unchanged.
        var id = await CreateListAsync(a, "Mine", mine);
        res = await a.PutAsJsonAsync($"/api/distribution-lists/{id}", new { name = "Mine", clientIds = new[] { theirs } });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var got = await a.GetFromJsonAsync<JsonElement>($"/api/distribution-lists/{id}");
        Assert.Equal(new[] { mine }, Ids(got.GetProperty("clientIds")));
    }

    [Fact]
    public async Task UserId_in_body_is_ignored()
    {
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var bId = (await b.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user").GetProperty("id").GetString();
        var res = await a.PostAsJsonAsync("/api/distribution-lists", new { name = "X", userId = bId, clientIds = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Single((await a.GetFromJsonAsync<JsonElement>("/api/distribution-lists")).EnumerateArray());
        Assert.Empty((await b.GetFromJsonAsync<JsonElement>("/api/distribution-lists")).EnumerateArray());
    }

    [Fact]
    public async Task Duplicate_client_ids_stored_once()
    {
        var user = await _app.LoginAsync("dana@example.com");
        var ruth = await AddClientAsync(user, "Ruth");
        var res = await user.PostAsJsonAsync("/api/distribution-lists", new { name = "Dup", clientIds = new[] { ruth, ruth, "" } });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(new[] { ruth }, Ids((await res.Json()).GetProperty("clientIds")));
    }

    [Fact]
    public async Task List_validation_errors()
    {
        var user = await _app.LoginAsync("dana@example.com");

        async Task<string?> Post(object body) => await (await user.PostAsJsonAsync("/api/distribution-lists", body)).Code();

        Assert.Equal("list_name_required", await Post(new { name = "  " }));
        Assert.Equal("list_name_required", await Post(new { name = new string('x', 61) }));
        Assert.Equal("list_description_too_long", await Post(new { name = "A", description = new string('x', 301) }));
        var tooMany = Enumerable.Range(0, DistributionList.MaxClients + 1).Select(_ => MongoDB.Bson.ObjectId.GenerateNewId().ToString());
        Assert.Equal("too_many_list_clients", await Post(new { name = "A", clientIds = tooMany }));

        var db = _app.Services.GetRequiredService<MongoContext>();
        var me = (await user.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user").GetProperty("id").GetString()!;
        var now = _app.Clock.Now.UtcDateTime;
        await db.DistributionLists.InsertManyAsync(Enumerable.Range(0, DistributionList.MaxPerUser)
            .Select(i => new DistributionList { UserId = me, Name = $"L{i}", CreatedAt = now, UpdatedAt = now }));
        var res = await user.PostAsJsonAsync("/api/distribution-lists", new { name = "One more" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("list_limit", await res.Code());
    }

    [Fact]
    public async Task Deleting_client_pulls_it_from_lists()
    {
        var user = await _app.LoginAsync("dana@example.com");
        var ruth = await AddClientAsync(user, "Ruth");
        var avi = await AddClientAsync(user, "Avi");
        var l1 = await CreateListAsync(user, "One", ruth, avi);
        var l2 = await CreateListAsync(user, "Two", ruth);

        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/clients/{ruth}")).StatusCode);

        var db = _app.Services.GetRequiredService<MongoContext>();
        Assert.Equal(new List<string> { avi }, (await db.DistributionLists.Find(l => l.Id == l1).FirstAsync()).ClientIds);
        Assert.Empty((await db.DistributionLists.Find(l => l.Id == l2).FirstAsync()).ClientIds);
    }

    [Fact]
    public async Task Regular_user_can_use_distribution_lists_api()
    {
        var user = await _app.LoginAsync("regular@example.com");
        var me = (await user.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user");
        Assert.Equal("user", me.GetProperty("role").GetString());
        await CreateListAsync(user, "Mine", await AddClientAsync(user, "Ruth"));
    }

    [Fact]
    public async Task Distribution_lists_require_terms_and_auth()
    {
        var anon = _app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/distribution-lists")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/api/distribution-lists", new { name = "X" })).StatusCode);

        var fresh = await _app.LoginAsync("fresh@example.com", ready: false);
        (await fresh.PutAsJsonAsync("/api/me", new { firstName = "Fresh", lastName = "One", phone = "050-1234567" }))
            .EnsureSuccessStatusCode();
        var res = await fresh.GetAsync("/api/distribution-lists");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("terms_required", await res.Code());
    }
}
