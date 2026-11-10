using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Core.Examples;
using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Sync;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class PublicPageTests(ApiFactory api)
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private record SignedIn(HttpClient Client, Guid PersonId, Guid CollectionId);

    private async Task<SignedIn> SignIn()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        return new SignedIn(client, me!.PersonId, me.Collections.Single().Id);
    }

    private Task AddMember(Guid collectionId, Guid personId, MemberRole role) =>
        api.WithDbAsync(async db =>
        {
            db.Memberships.Add(new Membership { CollectionId = collectionId, PersonId = personId, Role = role });
            await db.SaveChangesAsync();
        });

    private static async Task Push(SignedIn me, string kind, params object[] records)
    {
        var sent = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/records",
            new PushRequest([.. records.Select(r => new SyncRecord(kind, JsonSerializer.SerializeToElement(r, ApiFactory.Json)))]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.Empty((await sent.Content.ReadFromJsonAsync<PushResponse>(ApiFactory.Json))!.Refused);
    }

    private static async Task<Guid> AddPlant(SignedIn me, string nickname = "Mona", Guid? id = null, string? status = null,
        bool hidden = false, string? spare = null, bool deleted = false, Guid? cover = null, DateTimeOffset? created = null, string? notes = null)
    {
        var plantId = id ?? Guid.NewGuid();
        var at = created ?? Monday;
        await Push(me, "plants", new
        {
            id = plantId, createdAt = at, updatedAt = at, deletedAt = deleted ? at : (DateTimeOffset?)null, nickname, genus = "monstera", species = "deliciosa",
            status = status ?? "Active", hiddenFromPublicPage = hidden, spare, coverPhotoId = cover, notes,
        });
        return plantId;
    }

    private static async Task<Guid> AddCutting(SignedIn me, string nickname = "Mona cutting", string stage = "Started", bool hidden = false,
        int? spareCount = null, int initialCount = 1)
    {
        var id = Guid.NewGuid();
        await Push(me, "propagations", new
        {
            id, createdAt = Monday, updatedAt = Monday, nickname, stage, type = "Cutting", startedOn = "2026-09-20",
            hiddenFromPublicPage = hidden, spareCount, initialCount,
        });
        return id;
    }

    private static async Task<Guid> AddPhoto(SignedIn me, Guid subjectId, int hour = 9, bool uploaded = true, SubjectType type = SubjectType.Plant,
        int daysAgo = 5, bool hidden = false)
    {
        var id = Guid.NewGuid();
        var taken = Monday.AddDays(-daysAgo).AddHours(hour - 12);
        await Push(me, "photos", new
        {
            id, createdAt = taken, updatedAt = taken, subjectType = type, subjectId, takenAt = taken, width = 1600, height = 1200,
            hiddenFromPublicPage = hidden,
        });
        if (uploaded)
        {
            await Upload(me, id, "full");
            await Upload(me, id, "thumb");
        }
        return id;
    }

    private static async Task Upload(SignedIn me, Guid photoId, string size)
    {
        var bytes = Enumerable.Repeat((byte)0x42, 200).ToArray();
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var response = await me.Client.PutAsync($"/collections/{me.CollectionId}/photos/{photoId}/{size}", content);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Task AddNote(SignedIn me, Guid subjectId, string text, params Guid[] photos) =>
        Push(me, "timeline", new
        {
            id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday, subjectType = SubjectType.Plant, subjectId,
            occurredAt = Monday.AddDays(-3), kind = TimelineKind.Note, text, photoIds = photos,
        });

    private static string Path(SignedIn me) => $"/collections/{me.CollectionId}/public-page";

    private static Task<HttpResponseMessage> TurnOn(SignedIn me, string? language = "en") =>
        me.Client.PostAsJsonAsync(Path(me), new TurnOnPublicPageRequest("Europe/Copenhagen", language), ApiFactory.Json);

    private static async Task<PublicPageInfo> On(SignedIn me, string? language = "en")
    {
        var response = await TurnOn(me, language);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicPageInfo>(ApiFactory.Json))!;
    }

    private static async Task<PublicPageInfo> Get(SignedIn me) =>
        (await me.Client.GetFromJsonAsync<PublicPageInfo>(Path(me), ApiFactory.Json))!;

    private static UpdatePublicPageRequest Settings(
        string? title = null, string? line = null, PublicPageLayout layout = PublicPageLayout.Shelves, PublicPageOrder order = PublicPageOrder.Newest,
        bool cuttings = true, bool swap = false, bool wishlist = false, bool stories = false, bool searchable = false, string? language = null) =>
        new(title, line, layout, order, cuttings, swap, wishlist, stories, searchable, language, "Europe/Copenhagen");

    private static async Task<PublicPageInfo> Change(SignedIn me, UpdatePublicPageRequest settings)
    {
        var response = await me.Client.PutAsJsonAsync(Path(me), settings, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicPageInfo>(ApiFactory.Json))!;
    }

    private static Task<HttpResponseMessage> SetName(SignedIn me, string? name) =>
        me.Client.PutAsJsonAsync(Path(me) + "/name", new SetPublicPageNameRequest(name), ApiFactory.Json);

    private static async Task<PublicPageInfo> Named(SignedIn me, string name)
    {
        var response = await SetName(me, name);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicPageInfo>(ApiFactory.Json))!;
    }

    private static string TokenOf(PublicPageInfo page) => page.Url[(page.Url.LastIndexOf('/') + 1)..];

    private static string UniqueName() => "t" + Guid.NewGuid().ToString("N")[..12];

    private async Task<(HttpStatusCode Status, string Html, HttpResponseMessage Response)> Fetch(string path, HttpClient? client = null)
    {
        var response = await (client ?? api.CreateClient()).GetAsync(path);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), response);
    }

    private Task<(HttpStatusCode Status, string Html, HttpResponseMessage Response)> Front(PublicPageInfo page) => Fetch($"/s/{TokenOf(page)}");

    // Who can do what

    [Fact]
    public async Task An_editor_can_turn_the_page_on_and_gets_an_address_with_a_token_and_the_defaults()
    {
        var me = await SignIn();

        var page = await On(me, "da-DK");

        Assert.StartsWith($"{ApiFactory.ShareBaseUrl}/", page.Url);
        Assert.Equal(22, TokenOf(page).Length);
        Assert.Null(page.NamedUrl);
        Assert.Null(page.Name);
        Assert.Equal((PublicPageLayout.Shelves, PublicPageOrder.Newest), (page.Layout, page.Order));
        Assert.Equal((true, false, false, false, false), (page.ShowPropagations, page.ShowSwap, page.ShowWishlist, page.ShowStories, page.Searchable));
        Assert.Equal("da", page.Language);
        Assert.Equal(page.Id, (await Get(me)).Id);
    }

    [Fact]
    public async Task There_is_no_page_until_it_is_turned_on()
    {
        var me = await SignIn();

        Assert.Equal(HttpStatusCode.NotFound, (await me.Client.GetAsync(Path(me))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await me.Client.PutAsJsonAsync(Path(me), Settings(), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetName(me, "fernfriend")).StatusCode);
    }

    [Fact]
    public async Task A_viewer_can_read_the_settings_but_not_change_them()
    {
        var owner = await SignIn();
        await On(owner);
        var viewer = await SignIn();
        await AddMember(owner.CollectionId, viewer.PersonId, MemberRole.Viewer);
        var asViewer = viewer with { CollectionId = owner.CollectionId };

        Assert.Equal(HttpStatusCode.OK, (await asViewer.Client.GetAsync(Path(asViewer))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TurnOn(asViewer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asViewer.Client.PutAsJsonAsync(Path(asViewer), Settings(), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetName(asViewer, "fernfriend")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asViewer.Client.DeleteAsync(Path(asViewer))).StatusCode);
    }

    [Fact]
    public async Task Someone_outside_the_collection_gets_nothing()
    {
        var owner = await SignIn();
        await On(owner);
        var outsider = await SignIn();
        var asOutsider = outsider with { CollectionId = owner.CollectionId };

        Assert.Equal(HttpStatusCode.Forbidden, (await asOutsider.Client.GetAsync(Path(asOutsider))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TurnOn(asOutsider)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asOutsider.Client.PutAsJsonAsync(Path(asOutsider), Settings(), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetName(asOutsider, "fernfriend")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asOutsider.Client.DeleteAsync(Path(asOutsider))).StatusCode);
    }

    [Fact]
    public async Task Without_signing_in_the_app_endpoints_answer_401()
    {
        var me = await SignIn();
        var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path(me))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Path(me), new TurnOnPublicPageRequest(null, null), ApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(Path(me))).StatusCode);
    }

    // Turning it on

    [Fact]
    public async Task Turning_it_on_twice_gives_the_same_page()
    {
        var me = await SignIn();
        var first = await On(me);

        var again = await TurnOn(me, "da");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var page = (await again.Content.ReadFromJsonAsync<PublicPageInfo>(ApiFactory.Json))!;
        Assert.Equal((first.Id, first.Url, first.Language), (page.Id, page.Url, page.Language));
        await api.WithDbAsync(async db => Assert.Equal(1, await db.PublicPages.CountAsync(p => p.CollectionId == me.CollectionId)));
    }

    [Fact]
    public async Task No_page_is_made_before_the_share_domain_is_set_up()
    {
        var me = await SignIn();
        using var unset = api.InEnvironment("Testing", new() { ["Share:BaseUrl"] = "" });
        var client = unset.CreateClient();
        client.DefaultRequestHeaders.Authorization = me.Client.DefaultRequestHeaders.Authorization;

        var response = await TurnOn(me with { Client = client });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task One_person_can_turn_on_five_pages_a_day_and_no_more()
    {
        var me = await SignIn();
        for (var i = 0; i < PublicPageRules.MaxTurnedOnPerPersonPerDay; i++)
        {
            await On(me);
            Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync(Path(me))).StatusCode);
        }

        var response = await TurnOn(me);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Changing_the_settings_replaces_them_and_checks_the_lengths()
    {
        var me = await SignIn();
        await On(me);

        var tooLong = await me.Client.PutAsJsonAsync(Path(me),
            Settings(title: new string('t', PublicPageRules.MaxTitleLength + 1), line: new string('l', PublicPageRules.MaxLineLength + 1)), ApiFactory.Json);
        var errors = (await tooLong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("line", out _));

        var changed = await Change(me, Settings(title: "  Ann plants ", line: "A line", layout: PublicPageLayout.PhotoWall, order: PublicPageOrder.Name,
            cuttings: false, swap: true, wishlist: true, stories: true, searchable: true, language: "da"));

        Assert.Equal(("Ann plants", "A line", PublicPageLayout.PhotoWall, PublicPageOrder.Name), (changed.Title, changed.Line, changed.Layout, changed.Order));
        Assert.Equal((false, true, true, true, true, "da"),
            (changed.ShowPropagations, changed.ShowSwap, changed.ShowWishlist, changed.ShowStories, changed.Searchable, changed.Language));

        var back = await Change(me, Settings());
        Assert.Equal((null, null, true, false, false, false), (back.Title, back.Line, back.ShowPropagations, back.ShowSwap, back.ShowStories, back.Searchable));
        Assert.Equal("da", back.Language);
    }

    // Names

    [Theory]
    [InlineData("ab", "TooShort")]
    [InlineData("fern garden", "Characters")]
    [InlineData("fern--garden", "Hyphens")]
    [InlineData("admin", "Reserved")]
    [InlineData("Stikling-official", "Reserved")]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345", "TooLong")]
    public async Task A_name_that_does_not_follow_the_rules_gives_400_with_the_problem(string name, string problem)
    {
        var me = await SignIn();
        await On(me);

        var response = await SetName(me, name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(problem, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("problem").GetString());
    }

    [Fact]
    public async Task A_name_is_set_in_lower_case_and_gives_a_second_address()
    {
        var me = await SignIn();
        var page = await On(me);
        var name = UniqueName();

        var named = await Named(me, "@" + name.ToUpperInvariant());

        Assert.Equal(name, named.Name);
        Assert.Equal($"{ApiFactory.ShareBaseUrl}/@{name}", named.NamedUrl);
        Assert.Equal(page.Url, named.Url);
        Assert.Equal(HttpStatusCode.OK, (await Fetch($"/s/@{name}")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Fetch($"/s/@{name.ToUpperInvariant()}")).Status);
    }

    [Fact]
    public async Task The_same_name_again_answers_200_and_changes_nothing()
    {
        var me = await SignIn();
        await On(me);
        var name = UniqueName();
        await Named(me, name);

        var again = await SetName(me, name);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        await api.WithDbAsync(async db => Assert.Single((await db.PublicPages.SingleAsync(p => p.CollectionId == me.CollectionId)).NameClaims));
    }

    [Fact]
    public async Task A_name_in_use_by_another_collection_gives_409()
    {
        var first = await SignIn();
        await On(first);
        var name = UniqueName();
        await Named(first, name);
        var second = await SignIn();
        await On(second);

        var response = await SetName(second, name);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_name_that_was_given_up_cannot_be_taken_again_by_anyone_and_the_answer_is_the_same()
    {
        var first = await SignIn();
        await On(first);
        var name = UniqueName();
        await Named(first, name);
        var inUse = await SetName(await SignInWithPage(), name);

        var released = await SetName(first, null);
        var other = await SignInWithPage();
        var retired = await SetName(other, name);
        var again = await SetName(first, name);

        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        Assert.Null((await released.Content.ReadFromJsonAsync<PublicPageInfo>(ApiFactory.Json))!.Name);
        Assert.Equal(HttpStatusCode.Conflict, retired.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(
            (await inUse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(),
            (await retired.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    private async Task<SignedIn> SignInWithPage()
    {
        var me = await SignIn();
        await On(me);
        return me;
    }

    [Fact]
    public async Task A_retired_name_stays_retired_after_the_collection_is_deleted()
    {
        var first = await SignIn();
        await On(first);
        var name = UniqueName();
        await Named(first, name);
        await api.WithDbAsync(async db =>
        {
            await db.Collections.Where(c => c.Id == first.CollectionId).ExecuteDeleteAsync();
            Assert.False(await db.PublicPages.AnyAsync(p => p.CollectionId == first.CollectionId));
        });
        var other = await SignInWithPage();

        Assert.Equal(HttpStatusCode.Conflict, (await SetName(other, name)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await Fetch($"/s/@{name}")).Status);
    }

    [Fact]
    public async Task A_retired_name_stays_retired_after_the_account_is_deleted_and_the_page_goes_but_the_hash_stays()
    {
        var first = await SignIn();
        var page = await On(first);
        var name = UniqueName();
        await Named(first, name);

        Assert.Equal(HttpStatusCode.NoContent, (await first.Client.DeleteAsync("/me")).StatusCode);

        await api.WithDbAsync(async db =>
        {
            Assert.False(await db.PublicPages.AnyAsync(p => p.CollectionId == first.CollectionId));
            Assert.True(await db.PublicPageNames.AnyAsync(n => n.NameHash == PublicPageRules.NameHash(name)));
        });
        var other = await SignInWithPage();
        Assert.Equal(HttpStatusCode.Conflict, (await SetName(other, name)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Front(page)).Status);
        Assert.Equal(HttpStatusCode.Gone, (await Fetch($"/s/@{name}")).Status);
    }

    [Fact]
    public async Task A_page_can_change_its_name_three_times_a_day_and_not_a_fourth()
    {
        var me = await SignIn();
        await On(me);
        for (var i = 0; i < PublicPageRules.MaxNamesPerPagePerDay; i++)
            await Named(me, UniqueName());

        var fourth = await SetName(me, UniqueName());

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
    }

    [Fact]
    public async Task Claims_older_than_a_day_do_not_count()
    {
        var me = await SignIn();
        await On(me);
        for (var i = 0; i < PublicPageRules.MaxNamesPerPagePerDay; i++)
            await Named(me, UniqueName());
        await api.WithDbAsync(async db =>
        {
            var page = await db.PublicPages.SingleAsync(p => p.CollectionId == me.CollectionId);
            page.NameClaims = [.. page.NameClaims.Select(c => c.AddDays(-2))];
            await db.SaveChangesAsync();
        });

        var name = UniqueName();

        Assert.Equal(name, (await Named(me, name)).Name);
    }

    [Fact]
    public async Task The_old_name_answers_410_the_new_name_works_and_the_token_keeps_working()
    {
        var me = await SignIn();
        var page = await On(me);
        var old = UniqueName();
        var current = UniqueName();
        await Named(me, old);
        await Named(me, current);

        Assert.Equal(HttpStatusCode.Gone, (await Fetch($"/s/@{old}")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Fetch($"/s/@{current}")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Front(page)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/@{UniqueName()}")).Status);
    }

    // Turning it off

    [Fact]
    public async Task Turning_it_off_ends_the_address_for_good_retires_the_name_and_turning_on_again_makes_a_new_token()
    {
        var me = await SignIn();
        var page = await On(me);
        var name = UniqueName();
        await Named(me, name);

        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync(Path(me))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync(Path(me))).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await me.Client.GetAsync(Path(me))).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await Front(page)).Status);
        Assert.Equal(HttpStatusCode.Gone, (await Fetch($"/s/@{name}")).Status);
        Assert.Equal(HttpStatusCode.Gone, (await Fetch($"/s/{TokenOf(page)}/plants")).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await SetName(await SignInWithPage(), name)).StatusCode);

        var next = await On(me);
        Assert.NotEqual(TokenOf(page), TokenOf(next));
        Assert.Null(next.Name);
        Assert.Equal(HttpStatusCode.Gone, (await Front(page)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Front(next)).Status);
    }

    // What shows

    [Fact]
    public async Task The_front_page_shows_the_title_line_counts_and_plants_and_nothing_private()
    {
        var me = await SignIn();
        var plant = await AddPlant(me, "Mona", notes: "Bought from Jane for 40 kr");
        var photo = await AddPhoto(me, plant);
        var page = await On(me);
        await Change(me, Settings(title: "Ann plants", line: "My windowsill"));

        var (status, html, response) = await Front(page);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<title>Ann plants on Stikling</title>", html);
        Assert.Contains("<h1>Ann plants</h1>", html);
        Assert.Contains("My windowsill", html);
        Assert.Contains("1 plant", html);
        Assert.Contains("Mona", html);
        Assert.Contains("<i>Monstera deliciosa</i>", html);
        Assert.Contains($"/s/{TokenOf(page)}/photos/{photo}/thumb", html);
        Assert.Contains("loading=\"lazy\"", html);
        Assert.DoesNotContain("Jane", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.CacheControl!.NoCache);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task A_title_with_markup_is_written_out_as_text()
    {
        var me = await SignIn();
        var page = await On(me);
        await Change(me, Settings(title: "<script>alert(1)</script>"));
        await AddPlant(me, "<img src=x onerror=alert(2)>");

        var (_, html, _) = await Front(page);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    }

    [Fact]
    public async Task Hidden_died_given_away_sold_deleted_and_demo_plants_are_not_on_the_page()
    {
        var me = await SignIn();
        await AddPlant(me, "Shown plant");
        await AddPlant(me, "Hidden plant", hidden: true);
        await AddPlant(me, "Died plant", status: "Died");
        await AddPlant(me, "Given plant", status: "GivenAway");
        await AddPlant(me, "Sold plant", status: "Sold");
        await AddPlant(me, "Deleted plant", deleted: true);
        await AddPlant(me, "Demo coleus", id: ExampleFamily.MotherId);
        var page = await On(me);

        var (_, html, _) = await Front(page);

        Assert.Contains("Shown plant", html);
        foreach (var name in new[] { "Hidden plant", "Died plant", "Given plant", "Sold plant", "Deleted plant", "Demo coleus" })
            Assert.DoesNotContain(name, html);
        Assert.Contains("1 plant", html);
    }

    [Fact]
    public async Task The_cuttings_switch_works_and_only_active_cuttings_show()
    {
        var me = await SignIn();
        await AddPlant(me);
        await AddCutting(me, "Going cutting");
        await AddCutting(me, "Hidden cutting", hidden: true);
        await AddCutting(me, "Done cutting", stage: "Done");
        var page = await On(me);

        var (_, withCuttings, _) = await Front(page);
        Assert.Contains("Going cutting", withCuttings);
        Assert.Contains("1 plant · 1 cutting", withCuttings);
        Assert.DoesNotContain("Hidden cutting", withCuttings);
        Assert.DoesNotContain("Done cutting", withCuttings);
        Assert.Equal(HttpStatusCode.OK, (await Fetch($"/s/{TokenOf(page)}/cuttings")).Status);

        await Change(me, Settings(cuttings: false));
        var (_, without, _) = await Front(page);
        Assert.DoesNotContain("Going cutting", without);
        Assert.DoesNotContain("<h2>Cuttings</h2>", without);
        Assert.DoesNotContain("1 cutting", without);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/cuttings")).Status);
    }

    [Fact]
    public async Task The_wishlist_shows_names_only()
    {
        var me = await SignIn();
        await AddPlant(me);
        await Push(me, "wishes", new
        {
            id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday, name = "Pink princess", foundAt = "Greenhouse Hansen", notes = "ask for the variegated one",
            mostToPay = 777,
        });
        await Push(me, "wishes", new { id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday, name = "Already got it", gotOn = "2026-09-01" });
        var page = await On(me);
        await Change(me, Settings(wishlist: true));

        var (_, html, _) = await Front(page);
        var (status, part, _) = await Fetch($"/s/{TokenOf(page)}/wishlist");

        Assert.Contains("Pink princess", html);
        Assert.Contains("Pink princess", part);
        Assert.Equal(HttpStatusCode.OK, status);
        foreach (var text in new[] { html, part })
        {
            Assert.DoesNotContain("Greenhouse Hansen", text);
            Assert.DoesNotContain("variegated", text);
            Assert.DoesNotContain("777", text);
            Assert.DoesNotContain("Already got it", text);
        }

        await Change(me, Settings(wishlist: false));
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/wishlist")).Status);
    }

    [Fact]
    public async Task Up_for_swap_shows_a_plant_marked_for_swap_and_a_cutting_with_spare()
    {
        var me = await SignIn();
        await AddPlant(me, "Swap plant", spare: "UpForSwap");
        await AddPlant(me, "Keeper");
        await AddPlant(me, "Hidden swap plant", spare: "UpForSwap", hidden: true);
        await AddCutting(me, "Spare cutting", stage: "Rooted", spareCount: 1, initialCount: 2);
        var page = await On(me);
        await Change(me, Settings(cuttings: false, swap: true));

        var (_, front, _) = await Front(page);
        var (status, part, _) = await Fetch($"/s/{TokenOf(page)}/swap");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Up for swap", front);
        Assert.Contains("Swap plant", part);
        Assert.Contains("Spare cutting", part);
        Assert.Contains("1 · rooted", part);
        Assert.DoesNotContain("Hidden swap plant", part);
        Assert.DoesNotContain("Hidden swap plant", front);
    }

    [Fact]
    public async Task The_layouts_all_draw_the_page()
    {
        var me = await SignIn();
        await AddPlant(me);
        await AddCutting(me);
        var page = await On(me);

        var (_, shelves, _) = await Front(page);
        await Change(me, Settings(layout: PublicPageLayout.LongPage));
        var (_, longPage, _) = await Front(page);
        await Change(me, Settings(layout: PublicPageLayout.PhotoWall));
        var (_, wall, _) = await Front(page);
        var (_, wallPart, _) = await Fetch($"/s/{TokenOf(page)}/cuttings");

        Assert.Contains("pp-shelf", shelves);
        Assert.Contains("pp-grid", longPage);
        Assert.Contains("pp-wall", wall);
        Assert.Contains("pp-bar", wall);
        Assert.Contains("aria-current=\"page\"", wallPart);
        Assert.Contains("pp-bar", wallPart);
    }

    // Stories

    [Fact]
    public async Task With_stories_off_the_story_addresses_answer_404()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var cutting = await AddCutting(me);
        var page = await On(me);

        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/plants/{plant}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/cuttings/{cutting}")).Status);
        var (_, html, _) = await Front(page);
        Assert.DoesNotContain($"/plants/{plant}", html);
    }

    [Fact]
    public async Task With_stories_on_a_story_shows_milestones_and_photos_and_never_the_text_of_a_note()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var photo = await AddPhoto(me, plant);
        await AddNote(me, plant, "Paid 40 kr to Jane", photo);
        var page = await On(me);
        await Change(me, Settings(stories: true));

        var (_, front, _) = await Front(page);
        var (status, story, _) = await Fetch($"/s/{TokenOf(page)}/plants/{plant}");

        Assert.Contains($"/s/{TokenOf(page)}/plants/{plant}", front);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<h1>Mona</h1>", story);
        Assert.Contains($"/photos/{photo}/", story);
        Assert.DoesNotContain("Jane", story);
        Assert.DoesNotContain("40 kr", story);
        Assert.DoesNotContain("Were you given this one", story);
        Assert.DoesNotContain("/received/", story);
        Assert.Contains("Back to", story);
    }

    [Fact]
    public async Task A_story_of_something_that_does_not_show_answers_404()
    {
        var me = await SignIn();
        var hidden = await AddPlant(me, "Hidden", hidden: true);
        var died = await AddPlant(me, "Died", status: "Died");
        var hiddenCutting = await AddCutting(me, hidden: true);
        var page = await On(me);
        await Change(me, Settings(stories: true));

        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/plants/{hidden}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/plants/{died}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/cuttings/{hiddenCutting}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/plants/{Guid.NewGuid()}")).Status);
    }

    [Fact]
    public async Task A_hidden_photo_is_left_out_of_the_story()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var shown = await AddPhoto(me, plant, daysAgo: 6);
        var hidden = await AddPhoto(me, plant, daysAgo: 4, hidden: true);
        var page = await On(me);
        await Change(me, Settings(stories: true));

        var (_, story, _) = await Fetch($"/s/{TokenOf(page)}/plants/{plant}");

        Assert.Contains(shown.ToString(), story);
        Assert.DoesNotContain(hidden.ToString(), story);
    }

    // Photos

    private Task<HttpResponseMessage> Image(PublicPageInfo page, Guid photoId, string size = "full") =>
        api.CreateClient().GetAsync($"/s/{TokenOf(page)}/photos/{photoId}/{size}");

    [Fact]
    public async Task A_cover_is_served_and_other_photos_only_with_stories_on()
    {
        var me = await SignIn();
        var cover = Guid.NewGuid();
        var plant = await AddPlant(me, cover: cover);
        await Push(me, "photos", new
        {
            id = cover, createdAt = Monday, updatedAt = Monday, subjectType = SubjectType.Plant, subjectId = plant, takenAt = Monday.AddDays(-9),
            width = 1600, height = 1200,
        });
        await Upload(me, cover, "full");
        await Upload(me, cover, "thumb");
        var newer = await AddPhoto(me, plant, daysAgo: 2);
        var page = await On(me);

        Assert.Equal(HttpStatusCode.OK, (await Image(page, cover)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Image(page, cover, "thumb")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(page, newer)).StatusCode);

        await Change(me, Settings(stories: true));
        var served = await Image(page, newer);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("public, max-age=300", served.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task A_hidden_photo_reference_photo_and_a_photo_without_an_image_answer_404()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var hidden = await AddPhoto(me, plant, hidden: true);
        var waiting = await AddPhoto(me, plant, uploaded: false);
        var reference = Guid.NewGuid();
        await Push(me, "photos", new
        {
            id = reference, createdAt = Monday, updatedAt = Monday, subjectType = SubjectType.Plant, subjectId = plant, takenAt = Monday,
            width = 100, height = 100, isReference = true,
        });
        await Upload(me, reference, "full");
        var page = await On(me);
        await Change(me, Settings(stories: true));

        Assert.Equal(HttpStatusCode.NotFound, (await Image(page, hidden)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(page, waiting)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(page, reference)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(page, Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task The_photo_of_a_plant_that_is_hidden_or_in_another_collection_answers_404()
    {
        var me = await SignIn();
        var hiddenPlant = await AddPlant(me, "Hidden", hidden: true);
        var ofHidden = await AddPhoto(me, hiddenPlant);
        var page = await On(me);
        await Change(me, Settings(stories: true));
        var other = await SignIn();
        var otherPlant = await AddPlant(other);
        var ofOther = await AddPhoto(other, otherPlant);

        Assert.Equal(HttpStatusCode.NotFound, (await Image(page, ofHidden)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(page, ofOther)).StatusCode);
    }

    [Fact]
    public async Task A_rooting_photo_is_served_for_a_plant_potted_up_from_it_when_stories_are_on()
    {
        var me = await SignIn();
        var rooting = await AddCutting(me, "Rooting", stage: "Done");
        var photo = await AddPhoto(me, rooting, type: SubjectType.Propagation, daysAgo: 20);
        var plant = Guid.NewGuid();
        await Push(me, "plants", new
        {
            id = plant, createdAt = Monday, updatedAt = Monday, nickname = "Potted", fromPropagationId = rooting,
        });
        var page = await On(me);

        Assert.Equal(HttpStatusCode.NotFound, (await Image(page, photo)).StatusCode);

        await Change(me, Settings(stories: true));
        Assert.Equal(HttpStatusCode.OK, (await Image(page, photo)).StatusCode);
    }

    // Search engines and the sitemap

    [Fact]
    public async Task A_page_is_marked_noindex_unless_it_is_searchable()
    {
        var me = await SignIn();
        await AddPlant(me);
        var page = await On(me);

        var (_, html, response) = await Front(page);
        Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\">", html);

        await Change(me, Settings(searchable: true));
        var (_, listed, listedResponse) = await Front(page);
        Assert.False(listedResponse.Headers.Contains("X-Robots-Tag"));
        Assert.DoesNotContain("name=\"robots\"", listed);
        var (_, part, partResponse) = await Fetch($"/s/{TokenOf(page)}/plants");
        Assert.False(partResponse.Headers.Contains("X-Robots-Tag"));
        Assert.DoesNotContain("name=\"robots\"", part);
    }

    [Fact]
    public async Task A_page_with_a_name_points_search_engines_at_the_named_address_on_every_page()
    {
        var me = await SignIn();
        await AddPlant(me);
        var page = await On(me);
        var name = UniqueName();
        await Named(me, name);

        var (_, byToken, _) = await Front(page);
        var (_, plants, _) = await Fetch($"/s/{TokenOf(page)}/plants");
        var (_, byName, _) = await Fetch($"/s/@{name}");

        var canonical = $"<link rel=\"canonical\" href=\"{ApiFactory.ShareBaseUrl}/@{name}";
        Assert.Contains(canonical + "\"", byToken);
        Assert.Contains(canonical + "/plants\"", plants);
        Assert.Contains(canonical + "\"", byName);
    }

    [Fact]
    public async Task A_page_without_a_name_has_no_canonical_link()
    {
        var me = await SignIn();
        var page = await On(me);

        var (_, html, _) = await Front(page);

        Assert.DoesNotContain("rel=\"canonical\"", html);
    }

    [Fact]
    public async Task The_sitemap_lists_only_searchable_pages_with_their_parts_and_stories()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var listed = await On(me);
        var name = UniqueName();
        await Named(me, name);
        await Change(me, Settings(searchable: true, stories: true, wishlist: true));
        await Push(me, "wishes", new { id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday, name = "Wish" });
        var quiet = await SignIn();
        var unlisted = await On(quiet);
        var off = await SignIn();
        var turnedOff = await On(off);
        await Change(off, Settings(searchable: true));
        await off.Client.DeleteAsync(Path(off));

        var response = await api.CreateClient().GetAsync("/s/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=3600", response.Headers.CacheControl?.ToString());
        Assert.False(response.Headers.Contains("X-Robots-Tag"));
        var home = $"{ApiFactory.ShareBaseUrl}/@{name}";
        Assert.Contains($"<loc>{home}</loc>", xml);
        Assert.Contains($"<loc>{home}/plants</loc>", xml);
        Assert.Contains($"<loc>{home}/wishlist</loc>", xml);
        Assert.Contains($"<loc>{home}/plants/{plant}</loc>", xml);
        Assert.Contains("<lastmod>", xml);
        Assert.DoesNotContain(TokenOf(listed), xml);
        Assert.DoesNotContain(TokenOf(unlisted), xml);
        Assert.DoesNotContain(TokenOf(turnedOff), xml);
    }

    [Fact]
    public async Task Robots_txt_allows_everything_and_names_the_sitemap()
    {
        var response = await api.CreateClient().GetAsync("/s/robots.txt");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("User-agent: *", text);
        Assert.Contains("Allow: /", text);
        Assert.Contains($"Sitemap: {ApiFactory.ShareBaseUrl}/sitemap.xml", text);
        Assert.False(response.Headers.Contains("X-Robots-Tag"));
    }

    // The share host

    private HttpClient OnShareHost() =>
        api.InEnvironment("Testing", new() { ["Share:Host"] = "share.stikling.app" })
            .CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://share.stikling.app/"), AllowAutoRedirect = false });

    [Fact]
    public async Task On_the_share_host_the_name_is_the_whole_path()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var photo = await AddPhoto(me, plant);
        var page = await On(me);
        var name = UniqueName();
        await Named(me, name);
        await Change(me, Settings(stories: true, searchable: true));
        var client = OnShareHost();

        var (status, html, _) = await Fetch($"/@{name}", client);
        var (plantsStatus, _, _) = await Fetch($"/@{name}/plants", client);
        var (storyStatus, _, _) = await Fetch($"/@{name}/plants/{plant}", client);
        var image = await client.GetAsync($"/@{name}/photos/{photo}/full");
        var byToken = await Fetch($"/{TokenOf(page)}", client);
        var sitemap = await client.GetAsync("/sitemap.xml");
        var robots = await client.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"srcset=\"/@{name}/photos/{photo}/thumb ", html);
        Assert.Contains($"href=\"/@{name}/plants/{plant}\"", html);
        Assert.Equal(HttpStatusCode.OK, plantsStatus);
        Assert.Equal(HttpStatusCode.OK, storyStatus);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byToken.Status);
        Assert.Equal(HttpStatusCode.OK, sitemap.StatusCode);
        Assert.Equal(HttpStatusCode.OK, robots.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/@{UniqueName()}", client)).Status);
    }

    // Paging

    [Fact]
    public async Task A_part_has_48_tiles_to_a_page_with_numbered_links()
    {
        var me = await SignIn();
        var added = Monday;
        var records = Enumerable.Range(0, 50).Select(i => (object)new
        {
            id = Guid.NewGuid(), createdAt = added.AddMinutes(-i), updatedAt = added, nickname = $"Plant {i:00}", status = "Active",
        }).ToArray();
        foreach (var chunk in records.Chunk(25))
            await Push(me, "plants", chunk);
        var page = await On(me);
        await Change(me, Settings(order: PublicPageOrder.Name));

        var (_, front, _) = await Front(page);
        var (_, one, _) = await Fetch($"/s/{TokenOf(page)}/plants");
        var (_, two, _) = await Fetch($"/s/{TokenOf(page)}/plants?page=2");

        Assert.Contains("50 plants", front);
        Assert.Contains("See all 50", front);
        Assert.Contains("Page 1 of 2", one);
        Assert.Contains("Plant 47", one);
        Assert.DoesNotContain("Plant 48", one);
        Assert.Contains($"href=\"/s/{TokenOf(page)}/plants?page=2\"", one);
        Assert.Contains("Page 2 of 2", two);
        Assert.Contains("Plant 48", two);
        Assert.Contains("Plant 49", two);
        Assert.DoesNotContain("Plant 47", two);
        Assert.Contains($"href=\"/s/{TokenOf(page)}/plants\"", two);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/plants?page=3")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/plants?page=0")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/plants?page=abc")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Fetch($"/s/{TokenOf(page)}/plants?page=-1")).Status);
    }

    [Fact]
    public async Task An_empty_part_that_is_on_still_has_a_page()
    {
        var me = await SignIn();
        var page = await On(me);

        var (status, html, _) = await Fetch($"/s/{TokenOf(page)}/cuttings");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Nothing to show here yet.", html);
    }

    // Language

    [Fact]
    public async Task A_page_set_to_danish_is_written_in_danish()
    {
        var me = await SignIn();
        await AddPlant(me);
        await AddPlant(me, "Bytteplante", spare: "UpForSwap");
        var page = await On(me, "da");
        await Change(me, Settings(swap: true));

        var (_, html, _) = await Front(page);
        var (_, plants, _) = await Fetch($"/s/{TokenOf(page)}/plants");

        Assert.Contains("<html lang=\"da\">", html);
        Assert.Contains("En plantesamling", html);
        Assert.Contains("2 planter", html);
        Assert.Contains("Til bytte", html);
        Assert.Contains("Start din egen", html);
        Assert.Contains("Sidst opdateret", html);
        Assert.Contains("Anmeld denne side", html);
        Assert.Contains("på Stikling", html);
        Assert.Contains("Tilbage til En plantesamling", plants);
        Assert.DoesNotContain("Start your own", html);
    }

    [Fact]
    public async Task A_page_set_to_english_stays_english_when_the_visitor_asks_for_danish()
    {
        var me = await SignIn();
        var page = await On(me, "en");
        var client = api.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da-DK");

        var (_, html, _) = await Fetch($"/s/{TokenOf(page)}", client);

        Assert.Contains("A plant collection", html);
        Assert.Contains("Start your own", html);
    }

    // Share links keep working beside it

    [Fact]
    public async Task A_share_link_token_still_opens_the_share_link_and_a_public_token_is_not_taken_for_one()
    {
        var me = await SignIn();
        var plant = await AddPlant(me, "Shared plant");
        var share = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/shares", new CreateShareLinkRequest(SubjectType.Plant, plant, null), ApiFactory.Json);
        var link = (await share.Content.ReadFromJsonAsync<ShareLinkInfo>(ApiFactory.Json))!;
        var page = await On(me);
        var linkToken = link.Url[(link.Url.LastIndexOf('/') + 1)..];

        var (_, linkHtml, _) = await Fetch($"/s/{linkToken}");
        var (_, pageHtml, _) = await Front(page);

        Assert.Contains("<h1>Shared plant</h1>", linkHtml);
        Assert.Contains("pp-head", pageHtml);
        Assert.DoesNotContain("pp-head", linkHtml);
    }
}
