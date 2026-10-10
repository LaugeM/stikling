using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Core.Models;
using Stikling.Core.Sharing;
using Stikling.Core.Sync;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class ShareLinkTests(ApiFactory api)
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

    private static async Task Push(SignedIn me, string kind, object record)
    {
        var sent = await me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/records",
            new PushRequest([new SyncRecord(kind, JsonSerializer.SerializeToElement(record, ApiFactory.Json))]), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.Empty((await sent.Content.ReadFromJsonAsync<PushResponse>(ApiFactory.Json))!.Refused);
    }

    private static async Task<Guid> AddPlant(SignedIn me)
    {
        var id = Guid.NewGuid();
        await Push(me, "plants", new
        {
            id, createdAt = Monday, updatedAt = Monday, nickname = "Mona", genus = "monstera", species = "deliciosa", cultivar = "Thai Constellation",
        });
        return id;
    }

    private static async Task<Guid> AddPhoto(SignedIn me, Guid subjectId, int hour = 9, bool uploaded = true, SubjectType type = SubjectType.Plant, int daysAgo = 5)
    {
        var id = Guid.NewGuid();
        var taken = Monday.AddDays(-daysAgo).AddHours(hour - 12);
        await Push(me, "photos", new
        {
            id, createdAt = taken, updatedAt = taken, subjectType = type, subjectId, takenAt = taken, width = 1600, height = 1200,
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

    private static Task<HttpResponseMessage> TurnOn(SignedIn me, Guid subjectId, SubjectType type = SubjectType.Plant, string? zone = null) =>
        me.Client.PostAsJsonAsync($"/collections/{me.CollectionId}/shares", new CreateShareLinkRequest(type, subjectId, zone), ApiFactory.Json);

    private static async Task<ShareLinkInfo> Share(SignedIn me, Guid subjectId, SubjectType type = SubjectType.Plant)
    {
        var response = await TurnOn(me, subjectId, type, "Europe/Copenhagen");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShareLinkInfo>(ApiFactory.Json))!;
    }

    private static string TokenOf(ShareLinkInfo link) => link.Url[(link.Url.LastIndexOf('/') + 1)..];

    private static Task<HttpResponseMessage> Update(SignedIn me, ShareLinkInfo link, bool showNotes = true, string? name = null,
        string? line = null, params Guid[] leftOut) =>
        me.Client.PutAsJsonAsync($"/collections/{me.CollectionId}/shares/{link.Id}",
            new UpdateShareLinkRequest(showNotes, name, line, leftOut, "Europe/Copenhagen"), ApiFactory.Json);

    private async Task<(HttpStatusCode Status, string Html, HttpResponseMessage Response)> Page(ShareLinkInfo link, HttpClient? client = null)
    {
        var response = await (client ?? api.CreateClient()).GetAsync($"/s/{TokenOf(link)}");
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), response);
    }

    private Task<HttpResponseMessage> Image(ShareLinkInfo link, Guid photoId, string size = "full") =>
        api.CreateClient().GetAsync($"/s/{TokenOf(link)}/photos/{photoId}/{size}");

    // Making a link

    [Fact]
    public async Task An_editor_can_turn_sharing_on_and_gets_an_address_with_a_token()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);

        var link = await Share(me, plant);

        Assert.StartsWith($"{ApiFactory.ShareBaseUrl}/", link.Url);
        Assert.Equal(22, TokenOf(link).Length);
        Assert.Equal(plant, link.SubjectId);
        Assert.True(link.ShowNotes);
        Assert.Null(link.Name);
        var listed = await me.Client.GetFromJsonAsync<List<ShareLinkInfo>>($"/collections/{me.CollectionId}/shares", ApiFactory.Json);
        Assert.Equal(link.Id, Assert.Single(listed!).Id);
    }

    [Fact]
    public async Task No_link_is_made_before_the_share_domain_is_set_up()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        using var unset = api.InEnvironment("Testing", new() { ["Share:BaseUrl"] = "" });
        var client = unset.CreateClient();
        client.DefaultRequestHeaders.Authorization = me.Client.DefaultRequestHeaders.Authorization;

        var response = await TurnOn(me with { Client = client }, plant);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_viewer_can_see_the_links_but_not_make_one()
    {
        var owner = await SignIn();
        var plant = await AddPlant(owner);
        var viewer = await SignIn();
        await AddMember(owner.CollectionId, viewer.PersonId, MemberRole.Viewer);
        var asViewer = viewer with { CollectionId = owner.CollectionId };

        var made = await TurnOn(asViewer, plant);
        var listed = await asViewer.Client.GetAsync($"/collections/{owner.CollectionId}/shares");

        Assert.Equal(HttpStatusCode.Forbidden, made.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
    }

    [Fact]
    public async Task Someone_outside_the_collection_gets_nothing()
    {
        var owner = await SignIn();
        var plant = await AddPlant(owner);
        var link = await Share(owner, plant);
        var outsider = await SignIn();
        var asOutsider = outsider with { CollectionId = owner.CollectionId };

        Assert.Equal(HttpStatusCode.Forbidden, (await TurnOn(asOutsider, plant)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asOutsider.Client.GetAsync($"/collections/{owner.CollectionId}/shares")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Update(asOutsider, link)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await asOutsider.Client.DeleteAsync($"/collections/{owner.CollectionId}/shares/{link.Id}")).StatusCode);
    }

    [Fact]
    public async Task Without_signing_in_the_app_endpoints_answer_401()
    {
        var me = await SignIn();

        var response = await api.CreateClient().GetAsync($"/collections/{me.CollectionId}/shares");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_subject_that_is_not_on_the_server_yet_gives_409_with_words_for_the_app()
    {
        var me = await SignIn();

        var response = await TurnOn(me, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("It isn't on the server yet. Sync and try again.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_plant_id_sent_as_a_propagation_is_not_found_on_the_server()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);

        Assert.Equal(HttpStatusCode.Conflict, (await TurnOn(me, plant, SubjectType.Propagation)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TurnOn(me, plant, SubjectType.Pot)).StatusCode);
    }

    [Fact]
    public async Task Turning_it_on_twice_gives_the_same_link()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);

        var first = await TurnOn(me, plant);
        var second = await TurnOn(me, plant);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<ShareLinkInfo>(ApiFactory.Json);
        var b = await second.Content.ReadFromJsonAsync<ShareLinkInfo>(ApiFactory.Json);
        Assert.Equal(a!.Id, b!.Id);
        Assert.Equal(a.Url, b.Url);
    }

    [Fact]
    public async Task A_collection_can_have_50_links_on_and_no_more()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        await api.WithDbAsync(async db =>
        {
            for (var i = 0; i < ShareLinkRules.MaxActivePerCollection; i++)
                db.ShareLinks.Add(Seeded(me.CollectionId, createdBy: null));
            await db.SaveChangesAsync();
        });

        var response = await TurnOn(me, plant);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task One_person_can_make_20_links_a_day_and_no_more()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        await api.WithDbAsync(async db =>
        {
            // Turned off, so they count towards the day but not towards the 50 that can be on
            for (var i = 0; i < ShareLinkRules.MaxCreatedPerPersonPerDay; i++)
            {
                var link = Seeded(me.CollectionId, me.PersonId);
                link.TurnedOffAt = DateTimeOffset.UtcNow;
                db.ShareLinks.Add(link);
            }
            await db.SaveChangesAsync();
        });

        var response = await TurnOn(me, plant);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Links_made_more_than_a_day_ago_do_not_count_towards_the_day()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        await api.WithDbAsync(async db =>
        {
            for (var i = 0; i < ShareLinkRules.MaxCreatedPerPersonPerDay; i++)
            {
                var link = Seeded(me.CollectionId, me.PersonId);
                link.CreatedAt = DateTimeOffset.UtcNow.AddDays(-2);
                link.TurnedOffAt = DateTimeOffset.UtcNow.AddDays(-1);
                db.ShareLinks.Add(link);
            }
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Created, (await TurnOn(me, plant)).StatusCode);
    }

    private static ShareLink Seeded(Guid collectionId, Guid? createdBy) => new()
    {
        Token = Guid.NewGuid().ToString("N"),
        CollectionId = collectionId,
        SubjectType = SubjectType.Plant,
        SubjectId = Guid.NewGuid(),
        CreatedBy = createdBy,
        CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
    };

    // Changing and turning off

    [Fact]
    public async Task Changing_a_link_checks_the_lengths_and_the_page_uses_the_new_name_and_line()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var link = await Share(me, plant);

        var tooLong = await Update(me, link, name: new string('a', ShareLinkRules.MaxNameLength + 1));
        var tooLongLine = await Update(me, link, line: new string('a', ShareLinkRules.MaxLineLength + 1));
        var tooMany = await Update(me, link, leftOut: [.. Enumerable.Range(0, ShareLinkRules.MaxLeftOutPhotos + 1).Select(_ => Guid.NewGuid())]);
        var ok = await Update(me, link, name: "  Mona & Co <b>  ", line: "  Three <i>leaves</i>.  ");

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLongLine.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var changed = await ok.Content.ReadFromJsonAsync<ShareLinkInfo>(ApiFactory.Json);
        Assert.Equal("Mona & Co <b>", changed!.Name);

        var (status, html, _) = await Page(link);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<title>Mona &amp; Co &lt;b&gt; on Stikling</title>", html);
        Assert.Contains("Three &lt;i&gt;leaves&lt;/i&gt;.", html);
        Assert.DoesNotContain("<b>", html);
        Assert.DoesNotContain("<i>leaves", html);
    }

    [Fact]
    public async Task A_blank_name_goes_back_to_the_apps_own_words()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var link = await Share(me, plant);
        await Update(me, link, name: "Custom name");

        await Update(me, link, name: "   ");

        var (_, html, _) = await Page(link);
        Assert.Contains("<h1>Mona</h1>", html);
        Assert.DoesNotContain("Custom name", html);
    }

    [Fact]
    public async Task An_unknown_time_zone_is_dropped()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var created = await TurnOn(me, plant, zone: "Mars/Olympus_Mons");
        var link = (await created.Content.ReadFromJsonAsync<ShareLinkInfo>(ApiFactory.Json))!;

        await api.WithDbAsync(async db => Assert.Null((await db.ShareLinks.SingleAsync(l => l.Id == link.Id)).TimeZone));
        await Update(me, link);
        await api.WithDbAsync(async db => Assert.Equal("Europe/Copenhagen", (await db.ShareLinks.SingleAsync(l => l.Id == link.Id)).TimeZone));
    }

    [Fact]
    public async Task A_link_in_another_collection_is_not_found_when_changed_or_turned_off()
    {
        var owner = await SignIn();
        var plant = await AddPlant(owner);
        var link = await Share(owner, plant);
        var other = await SignIn();

        Assert.Equal(HttpStatusCode.NotFound, (await Update(other, link)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.DeleteAsync($"/collections/{other.CollectionId}/shares/{link.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Page(link)).Status);
    }

    [Fact]
    public async Task A_turned_off_link_answers_410_and_so_do_its_images_and_it_can_not_be_changed()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var photo = await AddPhoto(me, plant);
        var link = await Share(me, plant);
        Assert.Equal(HttpStatusCode.OK, (await Image(link, photo)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync($"/collections/{me.CollectionId}/shares/{link.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync($"/collections/{me.CollectionId}/shares/{link.Id}")).StatusCode);

        var (status, html, _) = await Page(link);
        Assert.Equal(HttpStatusCode.Gone, status);
        Assert.Contains("This page isn't here any more. It may have been turned off by the person who shared it.", html);
        Assert.Contains("href=\"https://stikling.app/\"", html);
        Assert.Equal(HttpStatusCode.Gone, (await Image(link, photo)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Update(me, link)).StatusCode);
        Assert.Empty((await me.Client.GetFromJsonAsync<List<ShareLinkInfo>>($"/collections/{me.CollectionId}/shares", ApiFactory.Json))!);
    }

    [Fact]
    public async Task Turning_sharing_on_again_makes_a_new_address_and_the_old_one_stays_off()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var first = await Share(me, plant);
        await me.Client.DeleteAsync($"/collections/{me.CollectionId}/shares/{first.Id}");

        var second = await Share(me, plant);

        Assert.NotEqual(first.Url, second.Url);
        Assert.Equal(HttpStatusCode.Gone, (await Page(first)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Page(second)).Status);
    }

    // The public page

    [Fact]
    public async Task An_unknown_token_gives_a_small_page_with_404()
    {
        var response = await api.CreateClient().GetAsync("/s/not-a-real-token");
        var oversized = await api.CreateClient().GetAsync($"/s/{new string('x', 500)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, oversized.StatusCode);
        Assert.Contains("isn't here any more", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_token_is_case_sensitive()
    {
        var me = await SignIn();
        var link = await Share(me, await AddPlant(me));
        var token = TokenOf(link);
        var swapped = new string([.. token.Select(c => char.IsLower(c) ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c))]);

        var response = await api.CreateClient().GetAsync($"/s/{swapped}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_deleted_subject_gives_404()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var link = await Share(me, plant);
        await Push(me, "plants", new { id = plant, createdAt = Monday, updatedAt = Monday.AddHours(1), deletedAt = Monday.AddHours(1) });

        var (status, _, _) = await Page(link);

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task The_page_shows_the_name_photos_and_notes_oldest_first_and_follows_new_ones()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var first = await AddPhoto(me, plant, hour: 9);
        await AddNote(me, plant, "Line one\nLine two", first);
        var link = await Share(me, plant);

        var (status, html, response) = await Page(link);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<title>Mona on Stikling</title>", html);
        Assert.Contains("<h1>Mona</h1>", html);
        Assert.Contains("<i>Monstera deliciosa</i>", html);
        Assert.Contains("&#x27;Thai Constellation&#x27;", html);
        Assert.Contains("1 photo · 1 note", html);
        // A line break is kept as a character reference, which the page shows as a break
        Assert.Contains("Line one&#xA;Line two", html);
        Assert.Contains($"/s/{TokenOf(link)}/photos/{first}/full", html);
        Assert.Contains($"{link.Url}/photos/{first}/full", html);
        Assert.Contains("loading=\"lazy\"", html);
        Assert.Contains("Kept in Stikling, a free app for your plants and cuttings.", html);
        Assert.Contains("Start your own", html);
        Assert.Contains("mailto:support@stikling.app?subject=Report%20a%20shared%20page&amp;body=", html);
        Assert.Contains(Uri.EscapeDataString(link.Url), html);
        Assert.Contains("public, max-age=60", response.Headers.CacheControl!.ToString());

        // A later photo and note appear after the owner syncs them
        var second = await AddPhoto(me, plant, hour: 15, daysAgo: 1);
        var (_, later, _) = await Page(link);
        Assert.Contains("2 photos", later);
        // The newest photo is the cover, so the order is checked in the history
        var history = later[later.IndexOf("class=\"history\"", StringComparison.Ordinal)..];
        Assert.True(history.IndexOf(first.ToString(), StringComparison.Ordinal) < history.IndexOf(second.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_cover_is_in_the_link_preview_with_its_size()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var photo = await AddPhoto(me, plant);
        var link = await Share(me, plant);

        var (_, html, _) = await Page(link);

        Assert.Contains($"<meta property=\"og:image\" content=\"{link.Url}/photos/{photo}/full\"", html);
        Assert.Contains("<meta property=\"og:image:width\" content=\"1600\"", html);
        Assert.Contains("<meta property=\"og:image:height\" content=\"1200\"", html);
        Assert.Contains($"<meta property=\"og:url\" content=\"{link.Url}\"", html);
        Assert.Contains("<meta property=\"og:type\" content=\"website\"", html);
        Assert.Contains("<meta property=\"og:site_name\" content=\"Stikling\"", html);
        Assert.Contains("<meta name=\"twitter:card\" content=\"summary_large_image\"", html);
        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\"", html);
        Assert.Contains("<meta name=\"viewport\"", html);
        Assert.Contains("fetchpriority=\"high\"", html);
        Assert.Matches("<img class=\"cover\"[^>]*width=\"1600\"[^>]*height=\"1200\"", html);
    }

    [Fact]
    public async Task A_photo_whose_image_has_not_arrived_yet_is_not_on_the_page()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var waiting = await AddPhoto(me, plant, uploaded: false);
        var link = await Share(me, plant);

        var (_, html, _) = await Page(link);

        Assert.DoesNotContain(waiting.ToString(), html);
    }

    [Fact]
    public async Task An_empty_history_still_shows_the_name_and_line()
    {
        var me = await SignIn();
        var link = await Share(me, await AddPlant(me));
        await Update(me, link, line: "Just started");

        var (status, html, _) = await Page(link);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<h1>Mona</h1>", html);
        Assert.Contains("Just started", html);
        Assert.Contains("name=\"description\" content=\"Just started\"", html);
    }

    [Fact]
    public async Task A_propagation_has_a_page_too()
    {
        var me = await SignIn();
        var id = Guid.NewGuid();
        await Push(me, "propagations", new
        {
            id, createdAt = Monday, updatedAt = Monday, nickname = "Mona cutting", type = "Cutting", medium = "Water",
            startedOn = "2026-08-01", stage = "Started",
        });
        var link = await Share(me, id, SubjectType.Propagation);

        var (status, html, _) = await Page(link);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<h1>Mona cutting</h1>", html);
        Assert.Contains("1 Aug 2026 · day 0", html);
    }

    [Fact]
    public async Task Nothing_private_is_on_the_page()
    {
        var me = await SignIn();
        var plant = Guid.NewGuid();
        var room = Guid.NewGuid();
        await Push(me, "places", new { id = room, createdAt = Monday, updatedAt = Monday, name = "Secret room" });
        await Push(me, "plants", new
        {
            id = plant, createdAt = Monday, updatedAt = Monday, nickname = "Mona", genus = "monstera", species = "deliciosa",
            source = "Secret shop", pricePaid = 123.45, placeId = room, status = "GivenAway", leftTo = "Secret buyer",
            leftFor = "Secret swap", notes = "Secret plant notes", tags = new[] { "secrettag" }, causeOfDeath = "Secret rot",
        });
        await Push(me, "timeline", new
        {
            id = Guid.NewGuid(), createdAt = Monday, updatedAt = Monday, subjectType = SubjectType.Plant, subjectId = plant,
            occurredAt = Monday.AddDays(-2), kind = TimelineKind.Change, text = "Moved to Secret room",
        });
        await AddPhoto(me, plant);
        var link = await Share(me, plant);

        var (status, html, _) = await Page(link);

        Assert.Equal(HttpStatusCode.OK, status);
        foreach (var secret in new[] { "Secret", "123.45", "123,45", "secrettag", "GivenAway", "Moved to" })
            Assert.DoesNotContain(secret, html);
    }

    [Fact]
    public async Task A_notes_text_shows_only_when_notes_are_on()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var photo = await AddPhoto(me, plant);
        await AddNote(me, plant, "Fresh leaf unfurled", photo);
        var link = await Share(me, plant);

        Assert.Contains("Fresh leaf unfurled", (await Page(link)).Html);
        await Update(me, link, showNotes: false);
        var (_, html, _) = await Page(link);

        Assert.DoesNotContain("Fresh leaf unfurled", html);
        Assert.Contains(photo.ToString(), html);
    }

    [Fact]
    public async Task A_left_out_photo_is_missing_from_the_page_and_its_image_gives_404()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var kept = await AddPhoto(me, plant, hour: 8);
        var hidden = await AddPhoto(me, plant, hour: 10);
        var link = await Share(me, plant);
        await Update(me, link, leftOut: hidden);

        var (_, html, _) = await Page(link);

        Assert.Contains(kept.ToString(), html);
        Assert.DoesNotContain(hidden.ToString(), html);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(link, hidden)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(link, hidden, "thumb")).StatusCode);

        var image = await Image(link, kept);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(200, (await image.Content.ReadAsByteArrayAsync()).Length);
        Assert.Equal("public, max-age=3600", image.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task A_photo_of_another_plant_gives_404_through_this_token()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var other = await AddPlant(me);
        var theirs = await AddPhoto(me, other);
        var link = await Share(me, plant);

        Assert.Equal(HttpStatusCode.NotFound, (await Image(link, theirs)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(link, Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task A_photo_from_another_collection_gives_404()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var link = await Share(me, plant);
        var other = await SignIn();
        var foreign = await AddPhoto(other, plant);

        Assert.Equal(HttpStatusCode.NotFound, (await Image(link, foreign)).StatusCode);
    }

    [Fact]
    public async Task A_deleted_photo_gives_404_and_an_image_that_is_not_there_gives_404()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var photo = await AddPhoto(me, plant);
        var waiting = await AddPhoto(me, plant, uploaded: false);
        var link = await Share(me, plant);
        await Push(me, "photos", new { id = photo, createdAt = Monday, updatedAt = Monday.AddHours(1), deletedAt = Monday.AddHours(1) });

        Assert.Equal(HttpStatusCode.NotFound, (await Image(link, photo)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Image(link, waiting)).StatusCode);
    }

    [Fact]
    public async Task The_page_is_a_hardened_page_without_scripts()
    {
        var me = await SignIn();
        var link = await Share(me, await AddPlant(me));

        var (_, html, response) = await Page(link);

        Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.StartsWith("default-src 'none'; img-src 'self'; style-src 'unsafe-inline';", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.DoesNotContain("script-src", policy);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_gone_page_has_the_same_headers()
    {
        var response = await api.CreateClient().GetAsync("/s/nothing");

        Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task A_name_with_markup_is_written_out_as_text()
    {
        var me = await SignIn();
        var plant = Guid.NewGuid();
        await Push(me, "plants", new { id = plant, createdAt = Monday, updatedAt = Monday, nickname = "<script>alert(1)</script>" });
        var link = await Share(me, plant);
        await AddNote(me, plant, "<img src=x onerror=alert(2)>");

        var (_, html, _) = await Page(link);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    }

    [Fact]
    public async Task There_is_no_robots_txt_that_disallows_anything()
    {
        var response = await api.CreateClient().GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The share host

    private HttpClient OnShareHost() =>
        api.InEnvironment("Testing", new() { ["Share:Host"] = "share.stikling.app" })
            .CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://share.stikling.app/"), AllowAutoRedirect = false });

    [Fact]
    public async Task On_the_share_host_the_token_is_the_whole_path()
    {
        var me = await SignIn();
        var plant = await AddPlant(me);
        var photo = await AddPhoto(me, plant);
        var link = await Share(me, plant);
        var client = OnShareHost();

        var page = await client.GetAsync($"/{TokenOf(link)}");
        var html = await page.Content.ReadAsStringAsync();
        var image = await client.GetAsync($"/{TokenOf(link)}/photos/{photo}/full");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("<h1>Mona</h1>", html);
        Assert.Contains($"src=\"/{TokenOf(link)}/photos/{photo}/full\"", html);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
    }

    [Fact]
    public async Task On_the_share_host_the_front_door_goes_to_the_app()
    {
        var client = OnShareHost();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://stikling.app/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task On_the_share_host_the_apps_endpoints_are_not_reachable()
    {
        var me = await SignIn();
        var client = OnShareHost();

        var response = await client.GetAsync($"/collections/{me.CollectionId}/shares");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task On_any_other_host_the_root_is_not_redirected()
    {
        var response = await api.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Limits for visitors

    private WebApplicationFactory<Program> Limited() => api.InEnvironment("Testing", new()
    {
        ["RequestLimits:PublicBurst"] = "3",
        ["RequestLimits:PublicPerMinute"] = "60",
    });

    private static Task<HttpResponseMessage> From(HttpClient client, string address, string path = "/s/nothing")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", address);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task One_address_going_over_the_limit_gets_429_with_when_to_try_again_and_others_are_not_slowed()
    {
        var client = Limited().CreateClient();

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 8; i++)
            responses.Add(await From(client, "203.0.113.7"));
        var someoneElse = await From(client, "203.0.113.8");

        Assert.All(responses.Take(3), r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        var refused = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.NotNull(refused);
        Assert.True(refused.Headers.RetryAfter?.Delta >= TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.NotFound, someoneElse.StatusCode);
    }

    [Fact]
    public async Task Only_the_address_the_ingress_added_counts()
    {
        var client = Limited().CreateClient();

        // The caller's own X-Forwarded-For comes first, and the ingress adds the address it saw last
        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 8; i++)
            responses.Add(await From(client, $"198.51.100.{i}, 203.0.113.9"));

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Requests_outside_the_share_pages_are_still_not_limited_without_a_sign_in()
    {
        var client = Limited().CreateClient();

        for (var i = 0; i < 10; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await From(client, "203.0.113.20", "/health")).StatusCode);
    }
}
