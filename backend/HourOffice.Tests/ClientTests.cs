using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HourOffice.Api.Data;
using HourOffice.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace HourOffice.Tests;

[Collection("api")]
public sealed class ClientTests(MongoFixture mongo) : IDisposable
{
    private readonly TestApp _app = new(mongo);
    public void Dispose() => _app.Dispose();

    // Clock: Monday 2026-11-09 08:00 Israel time.
    private const string Tomorrow = "2026-11-10";

    private static object NewClient(string first = "Ruth", string last = "Cohen", string[]? phones = null,
        string[]? emails = null, string? language = "he", bool consent = false, string notes = "") => new
    {
        firstName = first, lastName = last, phones = phones ?? new[] { "050-7654321" },
        emails = emails ?? Array.Empty<string>(), language, whatsAppConsent = consent, notes,
    };

    private static async Task<string> AddAsync(HttpClient c, object body)
    {
        var res = await c.PostAsJsonAsync("/api/clients", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Json()).GetProperty("clientId").GetString()!;
    }

    private static string[] Strings(JsonElement arr) => arr.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static async Task<JsonElement[]> ListAsync(HttpClient c) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/clients")).EnumerateArray().ToArray();

    [Fact]
    public async Task Create_list_update_delete_own_client()
    {
        var dana = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var id = await AddAsync(dana, NewClient(emails: new[] { "Ruth@Example.com" }, notes: " hi "));

        var got = await dana.GetFromJsonAsync<JsonElement>($"/api/clients/{id}");
        Assert.Equal(id, got.GetProperty("clientId").GetString());
        Assert.False(got.TryGetProperty("id", out _));
        Assert.False(got.TryGetProperty("userId", out _));
        Assert.Equal(new[] { "+972507654321" }, Strings(got.GetProperty("phones")));
        Assert.Equal(new[] { "ruth@example.com" }, Strings(got.GetProperty("emails")));
        Assert.Equal("hi", got.GetProperty("notes").GetString());

        var res = await dana.PutAsJsonAsync($"/api/clients/{id}", NewClient(first: "Rut", language: "en"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await res.Json();
        Assert.Equal("Rut", updated.GetProperty("firstName").GetString());
        Assert.Equal("en", updated.GetProperty("language").GetString());
        Assert.Single(await ListAsync(dana));

        Assert.Equal(HttpStatusCode.NoContent, (await dana.DeleteAsync($"/api/clients/{id}")).StatusCode);
        Assert.Empty(await ListAsync(dana));
        Assert.Equal("client_not_found", await (await dana.GetAsync($"/api/clients/{id}")).Code());
    }

    [Fact]
    public async Task List_is_sorted_by_last_then_first_name()
    {
        var dana = await _app.LoginAsync("dana@example.com");
        await AddAsync(dana, NewClient("Ben", "Zur"));
        await AddAsync(dana, NewClient("Avi", "Levi"));
        await AddAsync(dana, NewClient("Adi", "Levi"));
        var names = (await ListAsync(dana)).Select(c => c.GetProperty("firstName").GetString()).ToArray();
        Assert.Equal(new[] { "Adi", "Avi", "Ben" }, names);
    }

    [Fact]
    public async Task Other_user_cannot_read_update_or_delete()
    {
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var id = await AddAsync(a, NewClient("Secret"));

        var get = await b.GetAsync($"/api/clients/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal("client_not_found", await get.Code());
        Assert.Equal("client_not_found", await (await b.PutAsJsonAsync($"/api/clients/{id}", NewClient("Hacked"))).Code());
        Assert.Equal("client_not_found", await (await b.DeleteAsync($"/api/clients/{id}")).Code());

        var still = await a.GetFromJsonAsync<JsonElement>($"/api/clients/{id}");
        Assert.Equal("Secret", still.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task List_returns_only_own_clients()
    {
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        await AddAsync(a, NewClient("OfA"));
        await AddAsync(b, NewClient("OfB1"));
        await AddAsync(b, NewClient("OfB2"));

        Assert.Equal(new[] { "OfA" }, (await ListAsync(a)).Select(c => c.GetProperty("firstName").GetString()));
        Assert.Equal(2, (await ListAsync(b)).Length);
    }

    [Fact]
    public async Task Admin_cannot_see_other_users_clients()
    {
        var admin = await _app.AdminAsync();
        var user = await _app.LoginAsync("u@example.com");
        var id = await AddAsync(user, NewClient());

        Assert.Empty(await ListAsync(admin));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/clients/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/clients")).StatusCode);
    }

    [Fact]
    public async Task UserId_in_body_is_ignored()
    {
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var bId = (await b.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user").GetProperty("id").GetString();

        var res = await a.PostAsJsonAsync("/api/clients", new { firstName = "X", phones = new[] { "0501112222" }, userId = bId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Single(await ListAsync(a));
        Assert.Empty(await ListAsync(b));
    }

    [Fact]
    public async Task Same_details_allowed_on_many_clients()
    {
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var shared = NewClient(phones: new[] { "0501234567" }, emails: new[] { "same@example.com" });
        await AddAsync(a, shared);
        await AddAsync(a, shared);
        await AddAsync(b, shared);
        Assert.Equal(2, (await ListAsync(a)).Length);
    }

    [Fact]
    public async Task Same_phone_twice_in_one_client_stored_once()
    {
        var a = await _app.LoginAsync("a@example.com");
        var id = await AddAsync(a, NewClient(phones: new[] { "050-1234567", "+972 50 123 4567" },
            emails: new[] { "x@example.com", "X@EXAMPLE.com" }));
        var c = await a.GetFromJsonAsync<JsonElement>($"/api/clients/{id}");
        Assert.Equal(new[] { "+972501234567" }, Strings(c.GetProperty("phones")));
        Assert.Equal(new[] { "x@example.com" }, Strings(c.GetProperty("emails")));
    }

    [Fact]
    public async Task Two_phones_and_two_emails()
    {
        var a = await _app.LoginAsync("a@example.com");
        var id = await AddAsync(a, NewClient(phones: new[] { "0501111111", "+1 415 555 0100", "" },
            emails: new[] { "one@example.com", "two@example.com" }));
        var c = await a.GetFromJsonAsync<JsonElement>($"/api/clients/{id}");
        Assert.Equal(new[] { "+972501111111", "+14155550100" }, Strings(c.GetProperty("phones")));
        Assert.Equal(new[] { "one@example.com", "two@example.com" }, Strings(c.GetProperty("emails")));

        Assert.Equal("too_many_phones", await (await a.PostAsJsonAsync("/api/clients",
            NewClient(phones: new[] { "0501111111", "0502222222", "0503333333" }))).Code());
        Assert.Equal("too_many_emails", await (await a.PostAsJsonAsync("/api/clients",
            NewClient(emails: new[] { "a@x.com", "b@x.com", "c@x.com" }))).Code());
        Assert.Equal("phone_required", await (await a.PostAsJsonAsync("/api/clients",
            NewClient(phones: new[] { "", " " }))).Code());
    }

    [Fact]
    public async Task Validation_errors()
    {
        var a = await _app.LoginAsync("a@example.com");
        async Task<string?> Post(object body)
        {
            var res = await a.PostAsJsonAsync("/api/clients", body);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            return await res.Code();
        }
        Assert.Equal("client_name_required", await Post(NewClient(first: " ")));
        Assert.Equal("invalid_phone", await Post(NewClient(phones: new[] { "12345" })));
        Assert.Equal("invalid_email", await Post(NewClient(emails: new[] { "not-an-email" })));
        Assert.Equal("notes_too_long", await Post(NewClient(notes: new string('x', 501))));
        Assert.Empty(await ListAsync(a));
    }

    [Fact]
    public async Task Invalid_language()
    {
        var a = await _app.LoginAsync("a@example.com");
        Assert.Equal("invalid_language", await (await a.PostAsJsonAsync("/api/clients", NewClient(language: "fr"))).Code());
        var id = await AddAsync(a, NewClient(language: null));
        Assert.Equal("he", (await a.GetFromJsonAsync<JsonElement>($"/api/clients/{id}")).GetProperty("language").GetString());
    }

    [Theory]
    [InlineData("050-123-4567", "+972501234567")]
    [InlineData("00972501234567", "+972501234567")]
    [InlineData("+972 50 123 4567", "+972501234567")]
    [InlineData("00 44 20 7946 0958", "+442079460958")]
    [InlineData("+1 (415) 555-0100", "+14155550100")]
    [InlineData("+1234567", null)]
    [InlineData("+0123456789", null)]
    [InlineData("+1234567890123456", null)]
    [InlineData("12345", null)]
    [InlineData("", null)]
    public void Phone_is_normalized(string input, string? expected) => Assert.Equal(expected, Phone.Normalize(input));

    [Fact]
    public async Task Consent_timestamp_set_by_server()
    {
        var a = await _app.LoginAsync("a@example.com");
        var id = await AddAsync(a, NewClient());
        Assert.Equal(JsonValueKind.Null,
            (await a.GetFromJsonAsync<JsonElement>($"/api/clients/{id}")).GetProperty("whatsAppConsentAt").ValueKind);

        var on = await (await a.PutAsJsonAsync($"/api/clients/{id}", NewClient(consent: true))).Json();
        Assert.True(on.GetProperty("whatsAppConsent").GetBoolean());
        Assert.Equal(_app.Clock.Now.UtcDateTime, on.GetProperty("whatsAppConsentAt").GetDateTime().ToUniversalTime());

        // Saving again with consent still on keeps the original time.
        _app.Clock.Now = _app.Clock.Now.AddHours(1);
        var again = await (await a.PutAsJsonAsync($"/api/clients/{id}", NewClient(consent: true))).Json();
        Assert.Equal(on.GetProperty("whatsAppConsentAt").GetDateTime(), again.GetProperty("whatsAppConsentAt").GetDateTime());

        var off = await (await a.PutAsJsonAsync($"/api/clients/{id}", NewClient(consent: false))).Json();
        Assert.False(off.GetProperty("whatsAppConsent").GetBoolean());
        Assert.Equal(JsonValueKind.Null, off.GetProperty("whatsAppConsentAt").ValueKind);
    }

    [Fact]
    public async Task Terms_required()
    {
        var c = await _app.LoginAsync("half@example.com", ready: false);
        (await c.PutAsJsonAsync("/api/me", new { firstName = "A", lastName = "B", phone = "0501234567" })).EnsureSuccessStatusCode();
        var res = await c.GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("terms_required", await res.Code());
        Assert.Equal("terms_required", await (await c.PostAsJsonAsync("/api/clients", NewClient())).Code());
    }

    [Fact]
    public async Task Unauthenticated_gets_401()
    {
        var anon = _app.CreateClient();
        var id = "650000000000000000000000";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/clients", NewClient())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/clients/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PutAsJsonAsync($"/api/clients/{id}", NewClient())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.DeleteAsync($"/api/clients/{id}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_user_deletes_their_clients()
    {
        var admin = await _app.AdminAsync();
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var aId = (await a.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user").GetProperty("id").GetString();
        await AddAsync(a, NewClient("OfA"));
        await AddAsync(a, NewClient("OfA2"));
        await AddAsync(b, NewClient("OfB"));
        var db = _app.Services.GetRequiredService<MongoContext>();
        Assert.Equal(2, await db.Clients.CountDocumentsAsync(c => c.UserId == aId));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{aId}")).StatusCode);

        Assert.Equal(0, await db.Clients.CountDocumentsAsync(c => c.UserId == aId));
        Assert.Single(await ListAsync(b));
    }

    // ---- Client on a booking ----

    private static object BookingReq(string office, string? clientId, string start = "09:00", string end = "10:00") =>
        new { officeId = office, date = Tomorrow, startTime = start, endTime = end, notes = "", clientId };

    private static async Task<JsonElement> MineAsync(HttpClient c) =>
        await c.GetFromJsonAsync<JsonElement>("/api/bookings/mine");

    [Fact]
    public async Task Booking_with_own_client_shows_name_in_mine()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var dana = await _app.LoginAsync("dana@example.com");
        var client = await AddAsync(dana, NewClient("Ruth", "Cohen"));

        var res = await dana.PostAsJsonAsync("/api/bookings", BookingReq(office, client));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal("Ruth Cohen", (await res.Json()).GetProperty("clientName").GetString());

        var b = (await MineAsync(dana)).GetProperty("upcoming")[0];
        Assert.Equal(client, b.GetProperty("clientId").GetString());
        Assert.Equal("Ruth Cohen", b.GetProperty("clientName").GetString());
    }

    [Fact]
    public async Task Mine_returns_clientId_per_booking()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var dana = await _app.LoginAsync("dana@example.com");
        var client = await AddAsync(dana, NewClient());
        await dana.PostAsJsonAsync("/api/bookings", BookingReq(office, client, "09:00", "10:00"));
        await dana.PostAsJsonAsync("/api/bookings", BookingReq(office, null, "11:00", "12:00"));

        var upcoming = (await MineAsync(dana)).GetProperty("upcoming").EnumerateArray().ToArray();
        Assert.Equal(2, upcoming.Length);
        Assert.Equal(client, upcoming[0].GetProperty("clientId").GetString());
        Assert.Equal(JsonValueKind.Null, upcoming[1].GetProperty("clientId").ValueKind);
        Assert.Equal(JsonValueKind.Null, upcoming[1].GetProperty("clientName").ValueKind);
    }

    [Fact]
    public async Task Booking_with_other_users_client_rejected()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var aClient = await AddAsync(a, NewClient());

        var res = await b.PostAsJsonAsync("/api/bookings", BookingReq(office, aClient));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("client_not_found", await res.Code());
        Assert.Equal(0, (await MineAsync(b)).GetProperty("upcoming").GetArrayLength());
    }

    [Fact]
    public async Task Set_change_remove_client_on_booking()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var a = await _app.LoginAsync("a@example.com");
        var b = await _app.LoginAsync("b@example.com");
        var c1 = await AddAsync(a, NewClient("One"));
        var c2 = await AddAsync(a, NewClient("Two"));
        var bClient = await AddAsync(b, NewClient("OfB"));
        var booking = (await (await a.PostAsJsonAsync("/api/bookings", BookingReq(office, null))).Json()).GetProperty("id").GetString();
        var url = $"/api/bookings/{booking}/client";

        var set = await (await a.PutAsJsonAsync(url, new { clientId = c1 })).Json();
        Assert.Equal("One Cohen", set.GetProperty("clientName").GetString());
        var changed = await (await a.PutAsJsonAsync(url, new { clientId = c2 })).Json();
        Assert.Equal(c2, changed.GetProperty("clientId").GetString());

        Assert.Equal("booking_not_found", await (await b.PutAsJsonAsync(url, new { clientId = bClient })).Code());
        Assert.Equal("client_not_found", await (await a.PutAsJsonAsync(url, new { clientId = bClient })).Code());
        Assert.Equal(c2, (await MineAsync(a)).GetProperty("upcoming")[0].GetProperty("clientId").GetString());

        var removed = await (await a.PutAsJsonAsync(url, new { clientId = (string?)null })).Json();
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("clientId").ValueKind);

        // Cancelled bookings can still be labelled.
        await a.PostAsync($"/api/bookings/{booking}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync(url, new { clientId = c1 })).StatusCode);
    }

    [Fact]
    public async Task Set_client_on_booking_requires_terms()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var a = await _app.LoginAsync("a@example.com");
        var client = await AddAsync(a, NewClient());
        var booking = (await (await a.PostAsJsonAsync("/api/bookings", BookingReq(office, null))).Json()).GetProperty("id").GetString();

        // A new terms version is published; A has not accepted it yet.
        (await admin.PutAsJsonAsync("/api/admin/terms", new { contentHe = "חדש", contentEn = "New" })).EnsureSuccessStatusCode();
        var res = await a.PutAsJsonAsync($"/api/bookings/{booking}/client", new { clientId = client });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("terms_required", await res.Code());
    }

    [Fact]
    public async Task Admin_booking_routes_hide_client()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var a = await _app.LoginAsync("a@example.com");
        var aId = (await a.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user").GetProperty("id").GetString();
        var client = await AddAsync(a, NewClient("Hidden"));
        var booking = (await (await a.PostAsJsonAsync("/api/bookings", BookingReq(office, client))).Json()).GetProperty("id").GetString();

        static void NoClient(string json)
        {
            Assert.DoesNotContain("clientId", json);
            Assert.DoesNotContain("clientName", json);
            Assert.DoesNotContain("Hidden", json);
        }
        NoClient(await admin.GetStringAsync("/api/admin/bookings"));
        NoClient(await admin.GetStringAsync($"/api/admin/users/{aId}/bookings"));
        var put = await admin.PutAsJsonAsync($"/api/admin/bookings/{booking}", new { notes = "edited by admin" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        NoClient(await put.Content.ReadAsStringAsync());
        NoClient(await a.GetStringAsync($"/api/offices/{office}/availability?date={Tomorrow}"));

        // The admin edit keeps the owner's client link.
        Assert.Equal(client, (await MineAsync(a)).GetProperty("upcoming")[0].GetProperty("clientId").GetString());
    }

    [Fact]
    public async Task Deleted_client_leaves_booking_without_name()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var a = await _app.LoginAsync("a@example.com");
        var client = await AddAsync(a, NewClient());
        await a.PostAsJsonAsync("/api/bookings", BookingReq(office, client));
        await a.DeleteAsync($"/api/clients/{client}");

        var b = (await MineAsync(a)).GetProperty("upcoming")[0];
        Assert.Equal(client, b.GetProperty("clientId").GetString());
        Assert.Equal(JsonValueKind.Null, b.GetProperty("clientName").ValueKind);
    }
}
