using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HourOffice.Tests;

[Collection("api")]
public sealed class BookingTests(MongoFixture mongo) : IDisposable
{
    private readonly TestApp _app = new(mongo);
    public void Dispose() => _app.Dispose();

    // Clock: Monday 2026-11-09 08:00 Israel time.
    private const string Tomorrow = "2026-11-10";

    private static object Req(string office, string date = Tomorrow, string start = "09:00", string end = "11:00") =>
        new { officeId = office, date, startTime = start, endTime = end, notes = "" };

    [Fact]
    public async Task Booking_requires_profile_and_signed_terms()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var c = await _app.LoginAsync("half@example.com", ready: false);

        Assert.Equal("profile_required", await (await c.PostAsJsonAsync("/api/bookings", Req(office))).Code());
        (await c.PutAsJsonAsync("/api/me", new { firstName = "A", lastName = "B", phone = "0501234567" })).EnsureSuccessStatusCode();
        Assert.Equal("terms_required", await (await c.PostAsJsonAsync("/api/bookings", Req(office))).Code());
    }

    [Fact]
    public async Task Server_computes_price_and_blocks_overlaps()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin, rate: 80);
        var dana = await _app.LoginAsync("dana@example.com", "Dana Levi");
        var noa = await _app.LoginAsync("noa@example.com", "Noa Cohen");

        var res = await dana.PostAsJsonAsync("/api/bookings", Req(office, start: "09:00", end: "12:00"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var b = await res.Json();
        Assert.Equal(3, b.GetProperty("durationHours").GetInt32());
        Assert.Equal(240m, b.GetProperty("totalPrice").GetDecimal());
        Assert.Equal("ILS", b.GetProperty("currency").GetString());

        // Partial overlap (11:00-13:00 shares 11:00) is rejected; adjacent (12:00-14:00) is fine.
        Assert.Equal("slot_taken", await (await noa.PostAsJsonAsync("/api/bookings", Req(office, start: "11:00", end: "13:00"))).Code());
        Assert.Equal(HttpStatusCode.Created, (await noa.PostAsJsonAsync("/api/bookings", Req(office, start: "12:00", end: "14:00"))).StatusCode);

        var avail = await noa.GetFromJsonAsync<JsonElement>($"/api/offices/{office}/availability?date={Tomorrow}");
        var booked = avail.GetProperty("bookedSlots").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(new[] { "09:00", "10:00", "11:00", "12:00", "13:00" }, booked);
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_hour_produce_exactly_one_booking()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var clients = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => _app.LoginAsync($"racer{i}@example.com")));

        var results = await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync("/api/bookings", Req(office, start: "15:00", end: "16:00"))));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Cancelling_frees_the_slot_and_records_when()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var c = await _app.LoginAsync("c@example.com");
        var id = (await (await c.PostAsJsonAsync("/api/bookings", Req(office))).Json()).GetProperty("id").GetString();

        var cancelled = await (await c.PostAsync($"/api/bookings/{id}/cancel", null)).Json();
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, cancelled.GetProperty("cancelledAt").ValueKind);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/bookings", Req(office))).StatusCode);

        var other = await _app.LoginAsync("other@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/bookings/{id}/cancel", null)).StatusCode);
    }

    [Theory]
    [InlineData("2026-11-08", "09:00", "10:00", "date_in_past")]
    [InlineData("2026-11-24", "09:00", "10:00", "too_far_ahead")]
    [InlineData("2026-11-09", "08:00", "09:00", "time_in_past")]
    [InlineData(Tomorrow, "07:00", "09:00", "outside_opening_hours")]
    [InlineData(Tomorrow, "21:00", "23:00", "outside_opening_hours")]
    [InlineData(Tomorrow, "10:00", "10:00", "end_before_start")]
    [InlineData(Tomorrow, "10:30", "11:00", "invalid_time")]
    [InlineData("10/11/2026", "10:00", "11:00", "invalid_date")]
    public async Task Rules_are_enforced_by_the_server(string date, string start, string end, string code)
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var c = await _app.LoginAsync("rules@example.com");
        var res = await c.PostAsJsonAsync("/api/bookings", Req(office, date, start, end));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(code, await res.Code());
    }

    [Fact]
    public async Task Fourteen_days_ahead_is_the_last_allowed_day()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var c = await _app.LoginAsync("far@example.com");
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/bookings", Req(office, "2026-11-23"))).StatusCode);
    }

    [Fact]
    public async Task Unavailable_office_cannot_be_booked_or_listed()
    {
        var admin = await _app.AdminAsync();
        var hidden = await TestApp.CreateOfficeAsync(admin, "Hidden", available: false);
        await TestApp.CreateOfficeAsync(admin, "Visible");
        var c = await _app.LoginAsync("u@example.com");
        var list = await c.GetFromJsonAsync<JsonElement>("/api/offices");
        Assert.Equal(new[] { "Visible" }, list.EnumerateArray().Select(o => o.GetProperty("name").GetString()).ToArray());
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/bookings", Req(hidden))).StatusCode);
    }

    [Fact]
    public async Task Office_list_ignores_removed_filters()
    {
        var admin = await _app.AdminAsync();
        await TestApp.CreateOfficeAsync(admin, "Small Room", capacity: 2);
        await TestApp.CreateOfficeAsync(admin, "Team Room", capacity: 6);
        await TestApp.CreateOfficeAsync(admin, "Hall", capacity: 20);
        var c = await _app.LoginAsync("s@example.com");
        async Task<string[]> Names(string q) => (await c.GetFromJsonAsync<JsonElement>("/api/offices" + q))
            .EnumerateArray().Select(o => o.GetProperty("name").GetString()!).Order().ToArray();
        // Search and capacity filters were removed: the parameters are ignored, not an error.
        var all = new[] { "Hall", "Small Room", "Team Room" };
        Assert.Equal(all, await Names(""));
        Assert.Equal(all, await Names("?size=small"));
        Assert.Equal(all, await Names("?search=room"));
    }

    [Fact]
    public async Task My_bookings_split_into_upcoming_and_past_with_completed_status()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var c = await _app.LoginAsync("m@example.com");
        (await c.PostAsJsonAsync("/api/bookings", Req(office, "2026-11-09", "10:00", "11:00"))).EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync("/api/bookings", Req(office, Tomorrow, "10:00", "11:00"))).EnsureSuccessStatusCode();

        _app.Clock.Now = _app.Clock.Now.AddHours(4); // 12:00 Israel: today's booking has ended (and the 60-min token expired).
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/bookings/mine")).StatusCode);
        c = await _app.LoginAsync("m@example.com", ready: false);
        var mine = await c.GetFromJsonAsync<JsonElement>("/api/bookings/mine");
        Assert.Equal(1, mine.GetProperty("upcoming").GetArrayLength());
        var past = mine.GetProperty("past")[0];
        Assert.Equal("completed", past.GetProperty("status").GetString());
    }
}
