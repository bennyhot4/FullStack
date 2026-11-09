using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HourOffice.Tests;

[Collection("api")]
public sealed class AdminTests(MongoFixture mongo) : IDisposable
{
    private readonly TestApp _app = new(mongo);
    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Recurring_booking_creates_matching_weekdays_and_skips_taken_hours()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var user = await _app.LoginAsync("weekly@example.com", "Weekly Person");
        var userId = (await user.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("user").GetProperty("id").GetString();

        // Someone already holds Tuesday 2026-11-17 10:00-11:00.
        var other = await _app.LoginAsync("blocker@example.com");
        (await other.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-17", startTime = "10:00", endTime = "11:00" }))
            .EnsureSuccessStatusCode();

        // Sundays (0) and Tuesdays (2) from Mon 2026-11-09 to Sun 2026-11-22: 11/10, 11/15, 11/17, 11/22.
        var res = await (await admin.PostAsJsonAsync("/api/admin/bookings/recurring", new
        {
            officeId = office, userId, startTime = "09:00", endTime = "11:00", days = new[] { 0, 2 },
            fromDate = "2026-11-09", toDate = "2026-11-22", notes = "weekly",
        })).Json();
        Assert.Equal(3, res.GetProperty("created").GetInt32());
        Assert.Equal(1, res.GetProperty("skipped").GetInt32());

        var mine = await user.GetFromJsonAsync<JsonElement>("/api/bookings/mine");
        Assert.Equal(3, mine.GetProperty("upcoming").GetArrayLength());
    }

    [Fact]
    public async Task Admin_edit_is_checked_for_overlaps()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var c = await _app.LoginAsync("e@example.com");
        (await c.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-10", startTime = "09:00", endTime = "10:00" })).EnsureSuccessStatusCode();
        var second = await (await c.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-10", startTime = "12:00", endTime = "13:00" })).Json();
        var id = second.GetProperty("id").GetString();

        var clash = await admin.PutAsJsonAsync($"/api/admin/bookings/{id}", new { startTime = "09:00", endTime = "10:00" });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var ok = await (await admin.PutAsJsonAsync($"/api/admin/bookings/{id}", new { startTime = "14:00", endTime = "17:00", bookerName = "Renamed" })).Json();
        Assert.Equal(150m, ok.GetProperty("totalPrice").GetDecimal());
        Assert.Equal("Renamed", ok.GetProperty("bookerName").GetString());
    }

    [Fact]
    public async Task Analytics_counts_confirmed_bookings_of_this_week_per_user()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin, rate: 100);
        var a = await _app.LoginAsync("a@example.com", "Alice A");
        var b = await _app.LoginAsync("b@example.com", "Bob B");
        (await a.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-10", startTime = "09:00", endTime = "12:00" })).EnsureSuccessStatusCode();
        (await b.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-11", startTime = "09:00", endTime = "10:00" })).EnsureSuccessStatusCode();
        var cancel = await (await b.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-12", startTime = "09:00", endTime = "10:00" })).Json();
        await b.PostAsync($"/api/bookings/{cancel.GetProperty("id").GetString()}/cancel", null);
        // Next week: outside "this week".
        (await b.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-17", startTime = "09:00", endTime = "10:00" })).EnsureSuccessStatusCode();

        var week = await admin.GetFromJsonAsync<JsonElement>("/api/admin/analytics?period=week");
        Assert.Equal("2026-11-09", week.GetProperty("from").GetString());
        Assert.Equal("2026-11-15", week.GetProperty("to").GetString());
        Assert.Equal(2, week.GetProperty("totalBookings").GetInt32());
        Assert.Equal(4, week.GetProperty("totalHours").GetInt32());
        Assert.Equal(400m, week.GetProperty("revenue").GetDecimal());
        Assert.Equal("Alice Tester", week.GetProperty("byUser")[0].GetProperty("fullName").GetString());

        var month = await admin.GetFromJsonAsync<JsonElement>("/api/admin/analytics?period=month");
        Assert.Equal(3, month.GetProperty("totalBookings").GetInt32());
    }

    [Fact]
    public async Task Deleting_a_user_deletes_their_bookings_but_not_yourself()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var c = await _app.LoginAsync("gone@example.com");
        (await c.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-10", startTime = "09:00", endTime = "10:00" })).EnsureSuccessStatusCode();
        var users = await admin.GetFromJsonAsync<JsonElement>("/api/admin/users");
        var gone = users.EnumerateArray().First(u => u.GetProperty("email").GetString() == "gone@example.com").GetProperty("id").GetString();
        var self = users.EnumerateArray().First(u => u.GetProperty("email").GetString() == TestApp.AdminEmail).GetProperty("id").GetString();

        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/users/{gone}/bookings")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{gone}")).StatusCode);
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonElement>("/api/admin/bookings")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/users/{self}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Users_list_shows_terms_status()
    {
        var admin = await _app.AdminAsync();
        await _app.LoginAsync("signed@example.com");
        await _app.LoginAsync("unsigned@example.com", ready: false);
        var users = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/users")).EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("termsAccepted").GetBoolean());
        Assert.True(users["signed@example.com"]);
        Assert.False(users["unsigned@example.com"]);
    }

    [Fact]
    public async Task Branding_and_banners_are_public_and_validated()
    {
        var admin = await _app.AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/settings/branding",
            new { title = "x", buttonsColor = "red" })).StatusCode);
        (await admin.PutAsJsonAsync("/api/admin/settings/branding", new
        {
            title = "Therapy Rooms", description = "Book a room", logo = "", buttonsColor = "#112233",
            backgroundColor = "", pagesColor = "#FFFFFF",
        })).EnsureSuccessStatusCode();
        var b1 = await (await admin.PostAsJsonAsync("/api/admin/settings/banners", new { value = "https://img/1.jpg" })).Json();
        await admin.PostAsJsonAsync("/api/admin/settings/banners", new { value = "https://img/2.jpg" });
        await admin.DeleteAsync($"/api/admin/settings/banners/{b1.GetProperty("id").GetString()}");

        var pub = await _app.CreateClient().GetFromJsonAsync<JsonElement>("/api/settings/public");
        var brand = pub.GetProperty("branding");
        Assert.Equal("Therapy Rooms", brand.GetProperty("title").GetString());
        Assert.Equal("#112233", brand.GetProperty("buttonsColor").GetString());
        Assert.Equal("#F3F6F9", brand.GetProperty("backgroundColor").GetString()); // empty falls back to default
        Assert.Equal(new[] { "https://img/2.jpg" }, pub.GetProperty("banners").EnumerateArray().Select(x => x.GetProperty("url").GetString()).ToArray());
        Assert.Equal("₪", pub.GetProperty("currency").GetProperty("displaySymbols").GetProperty("he").GetString());
    }

    [Fact]
    public async Task Image_upload_accepts_images_only()
    {
        var admin = await _app.AdminAsync();
        using var png = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        bytes.Headers.ContentType = new("image/png");
        png.Add(bytes, "file", "a.png");
        var ok = await (await admin.PostAsync("/api/admin/upload", png)).Json();
        var url = ok.GetProperty("fileUrl").GetString()!;
        Assert.EndsWith(".png", url);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(new Uri(url).PathAndQuery)).StatusCode);

        using var txt = new MultipartFormDataContent();
        var t = new ByteArrayContent("hi"u8.ToArray());
        t.Headers.ContentType = new("text/html");
        txt.Add(t, "file", "a.html");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/admin/upload", txt)).StatusCode);
    }

    [Fact]
    public async Task Deleting_an_office_cancels_its_future_bookings()
    {
        var admin = await _app.AdminAsync();
        var office = await TestApp.CreateOfficeAsync(admin);
        var c = await _app.LoginAsync("del@example.com");
        (await c.PostAsJsonAsync("/api/bookings", new { officeId = office, date = "2026-11-10", startTime = "09:00", endTime = "10:00" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/offices/{office}")).StatusCode);
        var mine = await c.GetFromJsonAsync<JsonElement>("/api/bookings/mine");
        Assert.Equal("cancelled", mine.GetProperty("past")[0].GetProperty("status").GetString());
    }
}
