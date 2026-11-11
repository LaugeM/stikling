using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Api.Tests;

// Comparing a friend's page with one's own list, and the swap request settings. Shares the helpers of PublicPageTests.
public partial class PublicPageTests
{
    private async Task<(HttpStatusCode Status, JsonElement Body, HttpResponseMessage Response)> Compare(string address, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/s/{address}/compare");
        request.Headers.Add("Origin", ApiFactory.AppOrigin);
        var response = await (client ?? api.CreateClient()).SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, response.IsSuccessStatusCode ? JsonDocument.Parse(text).RootElement.Clone() : default, response);
    }

    private static IEnumerable<string> Names(JsonElement body, string list) =>
        body.GetProperty("list").GetProperty(list).EnumerateArray().Select(e => e.GetProperty("name").GetString()!);

    [Fact]
    public async Task Compare_answers_what_the_page_shows_to_anyone_from_the_app_and_never_caches()
    {
        var me = await SignIn();
        var cover = Guid.NewGuid();
        var plant = await AddPlant(me, "Mona", cover: cover);
        await Push(me, "photos", new
        {
            id = cover, createdAt = Monday, updatedAt = Monday, subjectType = SubjectType.Plant, subjectId = plant, takenAt = Monday.AddDays(-9),
            width = 1600, height = 1200,
        });
        await Upload(me, cover, "full");
        await Upload(me, cover, "thumb");
        await AddPlant(me, "Hidden one", hidden: true);
        await AddPlant(me, "Gone one", status: "Sold");
        await AddPlant(me, "Deleted one", deleted: true);
        await AddPlant(me, "Spare one", spare: "UpForSwap");
        await AddCutting(me, "Mona cutting", stage: "Rooted", spareCount: 2, initialCount: 3);
        await AddCutting(me, "Hidden cutting", hidden: true);
        var page = await On(me);
        var name = UniqueName();
        await Named(me, name);
        await Change(me, Settings(swap: true, wishlist: false));

        foreach (var address in new[] { TokenOf(page), "@" + name, "@" + name.ToUpperInvariant() })
        {
            var (status, body, response) = await Compare(address);
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
            Assert.Equal(ApiFactory.AppOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal(["Mona cutting", "Spare one", "Mona"], Names(body, "plants"));
        }

        var (_, shown, _) = await Compare("@" + name);
        var plants = shown.GetProperty("list").GetProperty("plants").EnumerateArray().ToList();
        Assert.Equal("Cuttings", plants[0].GetProperty("spare").GetString());
        Assert.Equal((2, "Rooted", "Propagation"),
            (plants[0].GetProperty("spareCount").GetInt32(), plants[0].GetProperty("stage").GetString(), plants[0].GetProperty("type").GetString()));
        Assert.Equal("UpForSwap", plants[1].GetProperty("spare").GetString());
        Assert.Equal("None", plants[2].GetProperty("spare").GetString());
        Assert.Equal(("Monstera", "deliciosa"), (plants[2].GetProperty("genus").GetString(), plants[2].GetProperty("species").GetString()));
        Assert.Equal(cover, plants[2].GetProperty("cover").GetProperty("id").GetGuid());
        Assert.Equal("@" + name, shown.GetProperty("address").GetString());
        Assert.False(shown.GetProperty("list").GetProperty("wishlistShared").GetBoolean());
        Assert.Equal(JsonValueKind.Null, shown.GetProperty("list").GetProperty("swapContact").ValueKind);

        // The address of the cover is served, without signing in
        var url = shown.GetProperty("coverUrls").GetProperty(cover.ToString()).GetString()!;
        Assert.Equal($"{ApiFactory.ShareBaseUrl}/@{name}/photos/{cover}/thumb", url);
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync($"/s/@{name}/photos/{cover}/full")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync($"/s/@{name}/photos/{cover}/thumb")).StatusCode);
    }

    [Fact]
    public async Task The_cover_of_a_cutting_that_only_shows_under_swap_is_served()
    {
        var me = await SignIn();
        var cutting = await AddCutting(me, "Spare cutting", stage: "Rooted", spareCount: 1, initialCount: 2);
        var photo = await AddPhoto(me, cutting, type: SubjectType.Propagation);
        var page = await On(me);
        await Change(me, Settings(cuttings: false, swap: true));

        var (_, body, _) = await Compare(TokenOf(page));

        var tile = Assert.Single(body.GetProperty("list").GetProperty("plants").EnumerateArray());
        Assert.Equal(photo, tile.GetProperty("cover").GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync($"/s/{TokenOf(page)}/photos/{photo}/full")).StatusCode);
    }

    [Fact]
    public async Task Compare_has_the_wishes_by_name_only_while_the_wishlist_part_is_on()
    {
        var me = await SignIn();
        await AddPlant(me);
        await Push(me, "wishes", new
        {
            id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday, genus = "Hoya", species = "carnosa", cultivar = "Krimson Queen",
            foundAt = "Greenhouse Hansen", notes = "ask for the variegated one", mostToPay = 777,
        });
        await Push(me, "wishes", new { id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday, name = "Pink princess", notes = "secret note" });
        await Push(me, "wishes", new { id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday, name = "Already got it", gotOn = "2026-09-01" });
        var page = await On(me);

        var (_, off, _) = await Compare(TokenOf(page));
        Assert.False(off.GetProperty("list").GetProperty("wishlistShared").GetBoolean());
        Assert.Empty(Names(off, "wishes"));

        await Change(me, Settings(wishlist: true));
        var (_, on, _) = await Compare(TokenOf(page));
        var text = on.GetRawText();

        Assert.True(on.GetProperty("list").GetProperty("wishlistShared").GetBoolean());
        Assert.Equal(["Hoya carnosa 'Krimson Queen'", "Pink princess"], Names(on, "wishes").Order());
        var hoya = on.GetProperty("list").GetProperty("wishes").EnumerateArray().Single(w => w.GetProperty("genus").ValueKind == JsonValueKind.String);
        Assert.Equal(("carnosa", "Krimson Queen"), (hoya.GetProperty("species").GetString(), hoya.GetProperty("cultivar").GetString()));
        foreach (var hiddenText in new[] { "Greenhouse Hansen", "variegated", "777", "secret note", "Already got it" })
            Assert.DoesNotContain(hiddenText, text);
    }

    [Fact]
    public async Task Compare_answers_404_for_an_unknown_page_and_410_for_one_turned_off_or_a_name_given_up()
    {
        var me = await SignIn();
        var page = await On(me);
        var name = UniqueName();
        await Named(me, name);

        Assert.Equal(HttpStatusCode.NotFound, (await Compare(UniqueName())).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Compare("@" + UniqueName())).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Compare("@no")).Status);

        await SetName(me, null);
        Assert.Equal(HttpStatusCode.Gone, (await Compare("@" + name)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Compare(TokenOf(page))).Status);

        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync(Path(me))).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await Compare(TokenOf(page))).Status);
    }

    [Fact]
    public async Task Compare_works_on_the_share_host()
    {
        var me = await SignIn();
        await AddPlant(me, "Mona");
        await On(me);
        var name = UniqueName();
        await Named(me, name);

        var response = await OnShareHost().GetAsync($"/@{name}/compare");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"Mona\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_contact_shows_only_with_swap_requests_on_and_leaves_with_them()
    {
        var me = await SignIn();
        var page = await On(me);

        // Off: nothing on the page or in the compare, but the button to compare is there
        var (_, off, _) = await Compare(TokenOf(page));
        Assert.Equal(JsonValueKind.Null, off.GetProperty("list").GetProperty("swapContact").ValueKind);
        var (_, html0, _) = await Front(page);
        Assert.DoesNotContain("Open to swap requests", html0);
        Assert.Contains($"href=\"https://stikling.app/compare/{TokenOf(page)}\"", html0);
        Assert.Contains("Compare with my list", html0);

        var on = await Change(me, Settings() with { SwapRequests = true, SwapContact = " fern@example.com " });
        Assert.Equal((true, "fern@example.com"), (on.SwapRequests, on.SwapContact));
        var (_, shown, _) = await Compare(TokenOf(page));
        Assert.Equal("fern@example.com", shown.GetProperty("list").GetProperty("swapContact").GetString());
        var (_, html, _) = await Front(page);
        Assert.Contains("Open to swap requests", html);
        Assert.Contains("href=\"mailto:fern@example.com\"", html);

        // Turned off again: the contact stays stored but is not shown
        var offAgain = await Change(me, Settings() with { SwapRequests = false });
        Assert.Equal((false, "fern@example.com"), (offAgain.SwapRequests, offAgain.SwapContact));
        var (_, hidden, _) = await Compare(TokenOf(page));
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("list").GetProperty("swapContact").ValueKind);
        Assert.DoesNotContain("fern@example.com", (await Front(page)).Html);
    }

    [Fact]
    public async Task A_link_contact_goes_to_the_link_and_the_compare_button_uses_the_name()
    {
        var me = await SignIn();
        await On(me);
        var name = UniqueName();
        await Named(me, name);
        await Change(me, Settings() with { SwapRequests = true, SwapContact = "https://example.com/swap?x=1&y=2" });

        var (_, html, _) = await Fetch($"/s/@{name}");

        Assert.Contains("href=\"https://example.com/swap?x=1&amp;y=2\"", html);
        Assert.Contains($"href=\"https://stikling.app/compare/@{name}\"", html);
    }

    [Fact]
    public async Task The_swap_settings_are_left_as_they_are_when_not_sent_and_the_contact_can_be_cleared()
    {
        var me = await SignIn();
        await On(me);
        await Change(me, Settings() with { SwapRequests = true, SwapContact = "https://example.com/swap" });

        // An older version of the app sends neither
        var older = await Change(me, Settings(title: "Changed"));
        Assert.Equal(("Changed", true, "https://example.com/swap"), (older.Title, older.SwapRequests, older.SwapContact));
        var stored = await Get(me);
        Assert.Equal((true, "https://example.com/swap"), (stored.SwapRequests, stored.SwapContact));

        // The contact on its own can change while swap requests stay as they are
        var changed = await Change(me, Settings() with { SwapContact = "fern@example.com" });
        Assert.Equal((true, "fern@example.com"), (changed.SwapRequests, changed.SwapContact));

        // Turning them off and clearing the contact
        var cleared = await Change(me, Settings() with { SwapRequests = false, SwapContact = "   " });
        Assert.Equal((false, (string?)null), (cleared.SwapRequests, cleared.SwapContact));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("example.com")]
    [InlineData("not an address")]
    [InlineData("javascript:alert(1)")]
    public async Task A_contact_that_is_not_a_link_or_an_email_is_a_validation_error(string contact)
    {
        var me = await SignIn();
        await On(me);

        var response = await me.Client.PutAsJsonAsync(Path(me), Settings() with { SwapContact = contact }, ApiFactory.Json);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("swapContact", out _));
        Assert.Null((await Get(me)).SwapContact);
    }

    [Fact]
    public async Task A_contact_that_is_too_long_and_swap_requests_without_a_contact_are_validation_errors()
    {
        var me = await SignIn();
        await On(me);

        var tooLong = await me.Client.PutAsJsonAsync(Path(me),
            Settings() with { SwapContact = "https://example.com/" + new string('a', PublicPageRules.MaxContactLength) }, ApiFactory.Json);
        var none = await me.Client.PutAsJsonAsync(Path(me), Settings() with { SwapRequests = true }, ApiFactory.Json);
        var emptied = await me.Client.PutAsJsonAsync(Path(me), Settings() with { SwapRequests = true, SwapContact = " " }, ApiFactory.Json);

        foreach (var response in new[] { tooLong, none, emptied })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("swapContact", out _));
        }
        Assert.False((await Get(me)).SwapRequests);

        // With a contact stored, turning requests on needs none sent
        await Change(me, Settings() with { SwapContact = "fern@example.com" });
        Assert.True((await Change(me, Settings() with { SwapRequests = true })).SwapRequests);
    }
}
