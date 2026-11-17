using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Stikling.Api.Data;
using Stikling.Core.Calendar;
using Stikling.Core.Sync;
using static Stikling.Api.Collections.CollectionEndpoints;

namespace Stikling.Api.Tests;

[Collection(ApiCollection.Name)]
public class CalendarTests(ApiFactory api)
{
    private readonly PeopleSupport people = new(api);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static CalendarDaysRequest Days(params (int Offset, string Title)[] days) =>
        new([.. days.Select(d => new CalendarDay(Today.AddDays(d.Offset), d.Title, "Fern\n\nOpen Today: https://stikling.app/"))]);

    private static async Task<string> MakeLink(Who who)
    {
        var response = await who.Client.PostAsync("/me/calendar", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CalendarLinkResponse>(ApiFactory.Json))!.Token;
    }

    private static Task<HttpResponseMessage> Upload(Who who, Guid collectionId, CalendarDaysRequest request) =>
        who.Client.PutAsJsonAsync($"/collections/{collectionId}/calendar", request, ApiFactory.Json);

    // The feed needs no sign-in
    private Task<HttpResponseMessage> Feed(string token) => api.CreateClient().GetAsync($"/calendar/{token}.ics");

    private async Task<bool> IsOn(Who who) =>
        (await who.Client.GetFromJsonAsync<CalendarStatusResponse>("/me/calendar", ApiFactory.Json))!.On;

    private async Task<Data.CalendarFeed> RowOf(Who who)
    {
        Data.CalendarFeed? row = null;
        await api.WithDbAsync(async db => row = await db.CalendarFeeds.AsNoTracking().SingleAsync(f => f.PersonId == who.PersonId));
        return row!;
    }

    [Fact]
    public async Task The_status_is_off_until_a_link_is_made()
    {
        var me = await people.SignIn();

        Assert.False(await IsOn(me));
        await MakeLink(me);
        Assert.True(await IsOn(me));
    }

    [Fact]
    public async Task The_calendar_needs_a_sign_in_to_make_or_upload()
    {
        var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/me/calendar")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/me/calendar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync("/me/calendar")).StatusCode);
    }

    [Fact]
    public async Task A_new_link_has_a_well_formed_token_and_only_its_hash_is_kept()
    {
        var me = await people.SignIn();

        var token = await MakeLink(me);

        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        var row = await RowOf(me);
        Assert.Equal(Stikling.Api.Calendar.CalendarRules.Hash(token), row.TokenHash);
        Assert.DoesNotContain(token, row.TokenHash + row.DaysJson + row.FeedId + row.CollectionId);
        Assert.Equal("[]", row.DaysJson);
    }

    [Fact]
    public async Task Uploaded_days_are_served_as_a_calendar()
    {
        var me = await people.SignIn();
        var token = await MakeLink(me);

        var uploaded = await Upload(me, me.CollectionId, Days((1, "Stikling: Fern needs attention; soon, ok"), (3, "Stikling: Check the soil of Ficus")));
        var feed = await Feed(token);
        var text = await feed.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NoContent, uploaded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        Assert.Equal("text/calendar; charset=utf-8", feed.Content.Headers.ContentType!.ToString());
        Assert.Equal("no-cache", feed.Headers.CacheControl!.ToString());
        Assert.Equal("noindex, nofollow", string.Join(", ", feed.Headers.GetValues("X-Robots-Tag")));
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", text);
        Assert.Contains("SUMMARY:Stikling: Fern needs attention\\; soon\\, ok\r\n", text);
        Assert.Contains($"DTSTART;VALUE=DATE:{Today.AddDays(3):yyyyMMdd}\r\n", text);
        Assert.Equal(2, text.Split("BEGIN:VEVENT").Length - 1);
        Assert.DoesNotContain(token, text);
    }

    [Fact]
    public async Task Days_more_than_a_week_back_are_left_out_of_the_feed()
    {
        var me = await people.SignIn();
        var token = await MakeLink(me);
        await Upload(me, me.CollectionId, Days((-2, "Stikling: Recent")));
        await api.WithDbAsync(async db =>
        {
            var row = await db.CalendarFeeds.SingleAsync(f => f.PersonId == me.PersonId);
            row.DaysJson = Stikling.Api.Calendar.CalendarRules.WriteDays(
                [new CalendarDay(Today.AddDays(-9), "Stikling: Old", null), new CalendarDay(Today.AddDays(-2), "Stikling: Recent", null)]);
            await db.SaveChangesAsync();
        });

        var text = await (await Feed(token)).Content.ReadAsStringAsync();

        Assert.Contains("Stikling: Recent", text);
        Assert.DoesNotContain("Stikling: Old", text);
    }

    [Fact]
    public async Task A_head_request_works()
    {
        var me = await people.SignIn();
        var token = await MakeLink(me);

        var response = await api.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/calendar/{token}.ics"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task An_unknown_or_malformed_token_is_not_found()
    {
        var unknown = Stikling.Api.Calendar.CalendarRules.NewToken();

        Assert.Equal(HttpStatusCode.NotFound, (await Feed(unknown)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Feed("short")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Feed(new string('a', 44))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Feed(new string('+', 43))).StatusCode);
    }

    [Fact]
    public async Task A_new_link_replaces_the_old_one_and_keeps_the_days()
    {
        var me = await people.SignIn();
        var first = await MakeLink(me);
        await Upload(me, me.CollectionId, Days((1, "Stikling: Fern needs attention")));
        var before = await RowOf(me);

        var second = await MakeLink(me);
        var after = await RowOf(me);

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await Feed(first)).StatusCode);
        Assert.Contains("Fern needs attention", await (await Feed(second)).Content.ReadAsStringAsync());
        Assert.NotEqual(before.FeedId, after.FeedId);
        Assert.Equal(me.CollectionId, after.CollectionId);
        Assert.Equal(before.DaysJson, after.DaysJson);
    }

    [Fact]
    public async Task Turning_it_off_stops_the_link_and_is_fine_when_it_is_already_off()
    {
        var me = await people.SignIn();
        var token = await MakeLink(me);

        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync("/me/calendar")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await Feed(token)).StatusCode);
        Assert.False(await IsOn(me));
        Assert.Equal(HttpStatusCode.NoContent, (await me.Client.DeleteAsync("/me/calendar")).StatusCode);
    }

    [Fact]
    public async Task Uploading_days_without_a_link_is_not_found()
    {
        var me = await people.SignIn();

        Assert.Equal(HttpStatusCode.NotFound, (await Upload(me, me.CollectionId, Days((1, "Stikling: Fern")))).StatusCode);
    }

    [Fact]
    public async Task Uploading_for_a_collection_the_caller_is_not_in_is_forbidden()
    {
        var me = await people.SignIn();
        var other = await people.SignIn();
        await MakeLink(me);

        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(me, other.CollectionId, Days((1, "Stikling: Fern")))).StatusCode);
    }

    [Fact]
    public async Task Days_that_break_the_rules_are_turned_down()
    {
        var me = await people.SignIn();
        await MakeLink(me);

        var tooMany = new CalendarDaysRequest([.. Enumerable.Range(0, 32).Select(i => new CalendarDay(Today.AddDays(i), "Stikling: Fern", null))]);
        var longTitle = Days((1, "Stikling: " + new string('x', 200)));
        var empty = Days((1, ""));
        var duplicate = Days((1, "Stikling: A"), (1, "Stikling: B"));
        var tooLate = Days((40, "Stikling: Fern"));
        var tooEarly = Days((-3, "Stikling: Fern"));
        var longDescription = new CalendarDaysRequest([new CalendarDay(Today, "Stikling: Fern", new string('x', 2001))]);

        foreach (var request in new[] { tooMany, longTitle, empty, duplicate, tooLate, tooEarly, longDescription, new CalendarDaysRequest(null), new CalendarDaysRequest([null!]) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(me, me.CollectionId, request)).StatusCode);

        Assert.Equal("[]", (await RowOf(me)).DaysJson);
    }

    [Fact]
    public async Task Leaving_a_shared_collection_clears_the_days_uploaded_for_it()
    {
        var owner = await people.SignIn();
        var guest = await people.Join(owner, MemberRole.Editor);
        var token = await MakeLink(guest);
        await Upload(guest, guest.CollectionId, Days((1, "Stikling: Fern needs attention")));

        Assert.Equal(HttpStatusCode.NoContent, (await people.Leave(guest)).StatusCode);

        var row = await RowOf(guest);
        Assert.Equal("[]", row.DaysJson);
        Assert.Null(row.CollectionId);
        var feed = await Feed(token);
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        Assert.DoesNotContain("BEGIN:VEVENT", await feed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await Upload(guest, guest.OwnCollectionId, Days((2, "Stikling: Ficus")))).StatusCode);
    }

    [Fact]
    public async Task Leaving_a_collection_keeps_days_uploaded_for_another_one()
    {
        var owner = await people.SignIn();
        var guest = await people.Join(owner, MemberRole.Editor);
        await MakeLink(guest);
        await Upload(guest, guest.OwnCollectionId, Days((1, "Stikling: Fern needs attention")));

        Assert.Equal(HttpStatusCode.NoContent, (await people.Leave(guest)).StatusCode);

        var row = await RowOf(guest);
        Assert.Equal(guest.OwnCollectionId, row.CollectionId);
        Assert.Contains("Fern needs attention", row.DaysJson);
    }

    [Fact]
    public async Task Being_removed_clears_the_days_uploaded_for_the_collection()
    {
        var owner = await people.SignIn();
        var guest = await people.Join(owner, MemberRole.Editor);
        var token = await MakeLink(guest);
        await Upload(guest, guest.CollectionId, Days((1, "Stikling: Fern needs attention")));

        Assert.Equal(HttpStatusCode.NoContent, (await people.Remove(owner, guest)).StatusCode);

        var row = await RowOf(guest);
        Assert.Equal("[]", row.DaysJson);
        Assert.Null(row.CollectionId);
        Assert.Equal(HttpStatusCode.OK, (await Feed(token)).StatusCode);
    }
}
