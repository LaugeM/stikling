using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class SettingsTests(ApiFactory api)
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SettingsId = Guid.NewGuid();

    private static JsonElement Settings(DateTimeOffset updatedAt, string theme) =>
        JsonSerializer.SerializeToElement(new { id = SettingsId, updatedAt, theme }, ApiFactory.Json);

    private static async Task<string?> PutTheme(HttpClient client, DateTimeOffset updatedAt, string theme)
    {
        var response = await client.PutAsJsonAsync("/me/settings", Settings(updatedAt, theme), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("theme").GetString();
    }

    private static async Task<string?> GetTheme(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/me/settings")).GetProperty("theme").GetString();

    [Fact]
    public async Task There_are_none_until_a_device_sends_them()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var response = await client.GetAsync("/me/settings");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Settings_sent_from_one_device_reach_the_others()
    {
        var clerkUserId = ApiFactory.NewClerkUserId();
        var computer = api.ClientFor(clerkUserId);
        var phone = api.ClientFor(clerkUserId);

        await PutTheme(computer, Monday, "Dark");

        Assert.Equal("Dark", await GetTheme(phone));
    }

    [Fact]
    public async Task The_newest_settings_are_kept()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());
        await PutTheme(client, Monday.AddHours(1), "Dark");

        var kept = await PutTheme(client, Monday, "Light");

        Assert.Equal("Dark", kept);
        Assert.Equal("Dark", await GetTheme(client));
        Assert.Equal("Light", await PutTheme(client, Monday.AddHours(2), "Light"));
    }

    [Fact]
    public async Task Everyone_has_their_own()
    {
        var mine = api.ClientFor(ApiFactory.NewClerkUserId());
        var theirs = api.ClientFor(ApiFactory.NewClerkUserId());

        await PutTheme(mine, Monday, "Dark");
        await PutTheme(theirs, Monday, "Light");

        Assert.Equal("Dark", await GetTheme(mine));
        Assert.Equal("Light", await GetTheme(theirs));
    }

    [Fact]
    public async Task Settings_need_a_token()
    {
        var anonymous = api.CreateClient();

        var get = await anonymous.GetAsync("/me/settings");
        var put = await anonymous.PutAsJsonAsync("/me/settings", Settings(Monday, "Dark"), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Fact]
    public async Task Settings_need_an_id_and_an_updatedAt()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var put = await client.PutAsJsonAsync("/me/settings", new { theme = "Dark" }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Settings_changed_in_the_future_are_refused()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());

        var put = await client.PutAsJsonAsync("/me/settings", Settings(DateTimeOffset.UtcNow.AddDays(2), "Dark"), ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/me/settings")).StatusCode);
    }
}
