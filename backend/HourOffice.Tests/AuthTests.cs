using System.Net;
using System.Net.Http.Json;
using HourOffice.Api.Endpoints;

namespace HourOffice.Tests;

[Collection("api")]
public sealed class AuthTests(MongoFixture mongo) : IDisposable
{
    private readonly TestApp _app = new(mongo);
    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Google_sign_in_creates_user_and_admin_email_gets_admin_role()
    {
        var c = _app.CreateClient();
        var user = await (await c.PostAsJsonAsync("/api/auth/google", new { credential = "good:dana@example.com:Dana Levi" })).Json();
        Assert.Equal("user", user.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal("Dana", user.GetProperty("user").GetProperty("firstName").GetString());

        var admin = await (await c.PostAsJsonAsync("/api/auth/google", new { credential = $"good:{TestApp.AdminEmail}" })).Json();
        Assert.Equal("admin", admin.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Invalid_google_token_is_rejected()
    {
        var res = await _app.CreateClient().PostAsJsonAsync("/api/auth/google", new { credential = "forged" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_reports_missing_profile_then_missing_terms()
    {
        var c = await _app.LoginAsync("new@example.com", ready: false);
        var me = await c.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/me");
        Assert.True(me.GetProperty("needsProfile").GetBoolean());
        Assert.True(me.GetProperty("needsTerms").GetBoolean());

        (await c.PutAsJsonAsync("/api/me", new { firstName = "Noa", lastName = "Cohen", phone = "0521234567" })).EnsureSuccessStatusCode();
        me = await c.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/me");
        Assert.False(me.GetProperty("needsProfile").GetBoolean());
        Assert.Equal("+972521234567", me.GetProperty("user").GetProperty("phoneNumber").GetString());

        var stale = await c.PostAsJsonAsync("/api/me/accept-terms", new { version = 99 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        (await c.PostAsJsonAsync("/api/me/accept-terms", new { version = 1 })).EnsureSuccessStatusCode();
        me = await c.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/me");
        Assert.False(me.GetProperty("needsTerms").GetBoolean());
    }

    [Fact]
    public async Task Publishing_new_terms_requires_signing_again()
    {
        var user = await _app.LoginAsync("u1@example.com");
        var admin = await _app.AdminAsync();
        (await admin.PutAsJsonAsync("/api/admin/terms", new { contentHe = "## חדש", contentEn = "## New" })).EnsureSuccessStatusCode();
        var me = await user.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/me");
        Assert.True(me.GetProperty("needsTerms").GetBoolean());
        Assert.Equal(2, me.GetProperty("currentTermsVersion").GetInt32());
    }

    [Fact]
    public async Task Refresh_token_rotates_and_cannot_be_reused()
    {
        var c = _app.CreateClient();
        var login = await (await c.PostAsJsonAsync("/api/auth/google", new { credential = "good:r@example.com" })).Json();
        var refresh = login.GetProperty("refreshToken").GetString();

        var first = await c.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await c.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Invited_user_gets_the_role_on_first_sign_in()
    {
        var admin = await _app.AdminAsync();
        (await admin.PostAsJsonAsync("/api/admin/users/invite", new { email = "Boss@Example.com", role = "admin" })).EnsureSuccessStatusCode();
        var dup = await admin.PostAsJsonAsync("/api/admin/users/invite", new { email = "boss@example.com", role = "user" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        var c = _app.CreateClient();
        var login = await (await c.PostAsJsonAsync("/api/auth/google", new { credential = "good:boss@example.com:Big Boss" })).Json();
        Assert.Equal("admin", login.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal("active", login.GetProperty("user").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Regular_user_cannot_reach_admin_endpoints()
    {
        var c = await _app.LoginAsync("plain@example.com");
        foreach (var url in new[] { "/api/admin/users", "/api/admin/bookings", "/api/admin/offices", "/api/admin/analytics" })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync("/api/me")).StatusCode);
    }

    [Theory]
    [InlineData("050-123-4567", "+972501234567")]
    [InlineData("+972 50 123 4567", "+972501234567")]
    [InlineData("501234567", "+972501234567")]
    [InlineData("03-1234567", "+97231234567")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    public void Israeli_phone_numbers_are_normalized(string input, string? expected) =>
        Assert.Equal(expected, HourOffice.Api.Services.Phone.NormalizeIsraeli(input));
}
