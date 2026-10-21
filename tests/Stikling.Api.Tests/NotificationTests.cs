using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Stikling.Api.Data;
using Stikling.Api.Notifications;
using static Stikling.Api.People.MeEndpoints;

namespace Stikling.Api.Tests;

/// <summary>Stands in for the push services. Everything sent is kept, and an endpoint can be told to answer Gone or Failed.</summary>
public class FakePushSender : IPushSender
{
    public record Sent(PushTarget Target, string Payload, TimeSpan Ttl);

    private readonly object _lock = new();
    private readonly List<Sent> _sent = [];

    public Dictionary<string, PushResult> Answers { get; } = [];

    public List<Sent> Messages
    {
        get { lock (_lock) return [.. _sent]; }
    }

    public Task<PushResult> SendAsync(PushTarget target, string payloadJson, TimeSpan ttl, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _sent.Add(new Sent(target, payloadJson, ttl));
            return Task.FromResult(Answers.GetValueOrDefault(target.Endpoint, PushResult.Sent));
        }
    }

    public List<Sent> To(string endpoint) => Messages.Where(m => m.Target.Endpoint == endpoint).ToList();
}

public class MovableClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

[Collection(ApiCollection.Name)]
public class NotificationTests(ApiFactory api)
{
    private static string NewEndpoint() => $"https://push.example.test/send/{Guid.NewGuid():N}";

    private static object Subscription(string endpoint) => new { endpoint, p256dh = "BKey", auth = "authKey" };

    private record SignedIn(HttpClient Client, Guid PersonId);

    private async Task<SignedIn> SignIn()
    {
        var client = api.ClientFor(ApiFactory.NewClerkUserId());
        var me = await client.GetFromJsonAsync<MeResponse>("/me", ApiFactory.Json);
        return new SignedIn(client, me!.PersonId);
    }

    private static Task<HttpResponseMessage> Subscribe(HttpClient client, string endpoint) =>
        client.PutAsJsonAsync("/me/notifications/subscriptions", Subscription(endpoint));

    private async Task<List<string>> EndpointsOf(Guid personId)
    {
        var endpoints = new List<string>();
        await api.WithDbAsync(async db =>
            endpoints = await db.PushSubscriptions.Where(s => s.PersonId == personId).OrderBy(s => s.CreatedAt).Select(s => s.Endpoint).ToListAsync());
        return endpoints;
    }

    private static DateOnly Today(string zone = "Europe/Copenhagen") =>
        NotificationRules.LocalDay(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone));

    private static object Days(string zone, string notifyAt, params (DateOnly Day, string Text)[] days) =>
        new { timeZone = zone, notifyAt, days = days.Select(d => new { day = d.Day.ToString("yyyy-MM-dd"), text = d.Text }).ToArray() };

    // The key

    [Fact]
    public async Task The_public_key_is_given_without_a_sign_in()
    {
        var response = await api.CreateClient().GetAsync("/notifications/key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ApiFactory.Vapid.Public, body.GetProperty("publicKey").GetString());
    }

    [Fact]
    public async Task Without_keys_the_key_is_not_found_and_the_rest_says_not_set_up()
    {
        var noKeys = new Dictionary<string, string?> { ["Notifications:VapidPublicKey"] = "", ["Notifications:VapidPrivateKey"] = "" };
        using var factory = api.InEnvironment("Testing", noKeys);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", api.TokenFor(ApiFactory.NewClerkUserId()));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/notifications/key")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Subscribe(client, NewEndpoint())).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PutAsJsonAsync("/me/notifications/days", Days("Europe/Copenhagen", "08:00"))).StatusCode);
    }

    // Subscriptions

    [Fact]
    public async Task A_subscription_is_added_and_asking_again_updates_it()
    {
        var me = await SignIn();
        var endpoint = NewEndpoint();

        Assert.Equal(HttpStatusCode.NoContent, (await Subscribe(me.Client, endpoint)).StatusCode);
        var again = await me.Client.PutAsJsonAsync("/me/notifications/subscriptions", new { endpoint, p256dh = "BNew", auth = "authNew" });

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        await api.WithDbAsync(async db =>
        {
            var stored = await db.PushSubscriptions.SingleAsync(s => s.PersonId == me.PersonId);
            Assert.Equal(endpoint, stored.Endpoint);
            Assert.Equal("BNew", stored.P256dh);
            Assert.Equal("authNew", stored.Auth);
            Assert.Equal(NotificationRules.HashEndpoint(endpoint), stored.EndpointHash);
        });
    }

    [Fact]
    public async Task A_device_signed_in_to_another_account_moves_its_subscription()
    {
        var first = await SignIn();
        var second = await SignIn();
        var endpoint = NewEndpoint();
        await Subscribe(first.Client, endpoint);

        await Subscribe(second.Client, endpoint);

        Assert.Empty(await EndpointsOf(first.PersonId));
        Assert.Equal([endpoint], await EndpointsOf(second.PersonId));
    }

    [Fact]
    public async Task A_subscription_is_removed_and_removing_it_again_is_fine()
    {
        var me = await SignIn();
        var endpoint = NewEndpoint();
        await Subscribe(me.Client, endpoint);

        var removed = await me.Client.PostAsJsonAsync("/me/notifications/subscriptions/remove", new { endpoint });
        var again = await me.Client.PostAsJsonAsync("/me/notifications/subscriptions/remove", new { endpoint });

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Empty(await EndpointsOf(me.PersonId));
    }

    [Fact]
    public async Task Someone_else_cannot_remove_a_subscription()
    {
        var owner = await SignIn();
        var other = await SignIn();
        var endpoint = NewEndpoint();
        await Subscribe(owner.Client, endpoint);

        var response = await other.Client.PostAsJsonAsync("/me/notifications/subscriptions/remove", new { endpoint });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([endpoint], await EndpointsOf(owner.PersonId));
    }

    [Fact]
    public async Task A_person_has_at_most_ten_and_the_oldest_goes()
    {
        var me = await SignIn();
        var endpoints = Enumerable.Range(0, 11).Select(_ => NewEndpoint()).ToList();

        foreach (var endpoint in endpoints)
        {
            await Subscribe(me.Client, endpoint);
            await Task.Delay(20);
        }

        Assert.Equal(endpoints.Skip(1).ToList(), await EndpointsOf(me.PersonId));
    }

    [Theory]
    [InlineData("http://push.example.test/send/1")]
    [InlineData("not an address")]
    [InlineData("")]
    public async Task A_subscription_needs_an_https_endpoint(string endpoint)
    {
        var me = await SignIn();

        Assert.Equal(HttpStatusCode.BadRequest, (await Subscribe(me.Client, endpoint)).StatusCode);
    }

    [Fact]
    public async Task An_endpoint_over_2000_characters_is_turned_down()
    {
        var me = await SignIn();

        var response = await Subscribe(me.Client, "https://push.example.test/" + new string('a', 2000));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_endpoints_need_a_sign_in()
    {
        var anonymous = api.CreateClient();
        var endpoint = NewEndpoint();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Subscribe(anonymous, endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/me/notifications/subscriptions/remove", new { endpoint })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/me/notifications/days", Days("Europe/Copenhagen", "08:00"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/me/notifications/test", new { endpoint })).StatusCode);
    }

    // Uploading the days

    [Fact]
    public async Task The_days_are_kept_with_when_the_next_one_is_due()
    {
        var me = await SignIn();
        var tomorrow = Today().AddDays(1);

        var response = await me.Client.PutAsJsonAsync("/me/notifications/days",
            Days("Europe/Copenhagen", "08:15", (tomorrow.AddDays(1), "Second"), (tomorrow, "First")));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await api.WithDbAsync(async db =>
        {
            var schedule = await db.NotificationSchedules.SingleAsync(s => s.PersonId == me.PersonId);
            Assert.Equal(new TimeOnly(8, 15), schedule.NotifyAt);
            Assert.Equal(NotificationRules.At(tomorrow, new TimeOnly(8, 15), TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen")), schedule.NextSendAt);
            Assert.Equal(["First", "Second"], NotificationRules.ReadDays(schedule.DaysJson).Select(d => d.Text));
        });
    }

    [Fact]
    public async Task An_empty_list_is_valid_and_means_nothing_to_send()
    {
        var me = await SignIn();

        var response = await me.Client.PutAsJsonAsync("/me/notifications/days", Days("Europe/Copenhagen", "08:00"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await api.WithDbAsync(async db => Assert.Null((await db.NotificationSchedules.SingleAsync(s => s.PersonId == me.PersonId)).NextSendAt));
    }

    [Fact]
    public async Task No_time_clears_the_schedule_but_keeps_the_row_and_the_day_last_sent()
    {
        var me = await SignIn();
        await me.Client.PutAsJsonAsync("/me/notifications/days", Days("Europe/Copenhagen", "08:00", (Today(), "Today")));

        var response = await me.Client.PutAsJsonAsync("/me/notifications/days", new { timeZone = "Europe/Copenhagen", notifyAt = (string?)null, days = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await api.WithDbAsync(async db =>
        {
            var schedule = await db.NotificationSchedules.SingleAsync(s => s.PersonId == me.PersonId);
            Assert.Null(schedule.NextSendAt);
            Assert.Equal("[]", schedule.DaysJson);
        });
    }

    [Fact]
    public async Task Uploading_again_replaces_the_days_and_keeps_the_day_last_sent()
    {
        var me = await SignIn();
        var day = Today().AddDays(1);
        await me.Client.PutAsJsonAsync("/me/notifications/days", Days("Europe/Copenhagen", "08:00", (day, "Old")));
        await api.WithDbAsync(async db =>
        {
            await db.NotificationSchedules.Where(s => s.PersonId == me.PersonId).ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSentOn, day));
        });

        await me.Client.PutAsJsonAsync("/me/notifications/days", Days("Europe/Copenhagen", "09:00", (day, "New"), (day.AddDays(1), "Later")));

        await api.WithDbAsync(async db =>
        {
            var schedule = await db.NotificationSchedules.SingleAsync(s => s.PersonId == me.PersonId);
            Assert.Equal(["New", "Later"], NotificationRules.ReadDays(schedule.DaysJson).Select(d => d.Text));
            Assert.Equal(NotificationRules.At(day.AddDays(1), new TimeOnly(9, 0), TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen")), schedule.NextSendAt);
        });
    }

    [Fact]
    public async Task Days_are_turned_down_when_they_are_not_valid()
    {
        var me = await SignIn();
        var today = Today();
        var zone = "Europe/Copenhagen";
        var tooMany = Enumerable.Range(0, 32).Select(i => (today.AddDays(i - 1), "x")).ToArray();

        var cases = new Dictionary<string, object>
        {
            ["unknown time zone"] = Days("Mars/Olympus", "08:00", (today, "x")),
            ["not a quarter hour"] = Days(zone, "08:10", (today, "x")),
            ["not a time"] = Days(zone, "eight", (today, "x")),
            ["more than 31 days"] = Days(zone, "08:00", tooMany.Append((today.AddDays(40), "x")).ToArray()),
            ["too far ahead"] = Days(zone, "08:00", (today.AddDays(32), "x")),
            ["too long ago"] = Days(zone, "08:00", (today.AddDays(-2), "x")),
            ["text too long"] = Days(zone, "08:00", (today, new string('a', 401))),
            ["day twice"] = Days(zone, "08:00", (today, "a"), (today, "b")),
        };

        foreach (var (name, body) in cases)
        {
            var response = await me.Client.PutAsJsonAsync("/me/notifications/days", body);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{name} answered {response.StatusCode}");
        }

        await api.WithDbAsync(async db => Assert.False(await db.NotificationSchedules.AnyAsync(s => s.PersonId == me.PersonId)));
    }

    [Fact]
    public async Task The_edges_of_what_is_allowed_are_taken()
    {
        var me = await SignIn();
        var today = Today();

        var response = await me.Client.PutAsJsonAsync("/me/notifications/days",
            Days("Europe/Copenhagen", "23:45", (today.AddDays(-1), new string('a', 400)), (today.AddDays(31), "last")));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // When a day is due, across time zones and daylight saving

    [Theory]
    [InlineData("2026-07-01", "08:00", "Europe/Copenhagen", "2026-07-01T06:00:00Z")]
    [InlineData("2026-12-01", "08:00", "Europe/Copenhagen", "2026-12-01T07:00:00Z")]
    [InlineData("2026-12-01", "08:00", "America/New_York", "2026-12-01T13:00:00Z")]
    [InlineData("2026-03-28", "08:00", "Europe/Copenhagen", "2026-03-28T07:00:00Z")]
    [InlineData("2026-03-29", "08:00", "Europe/Copenhagen", "2026-03-29T06:00:00Z")]
    [InlineData("2026-10-25", "08:00", "Europe/Copenhagen", "2026-10-25T07:00:00Z")]
    [InlineData("2026-03-29", "02:30", "Europe/Copenhagen", "2026-03-29T01:30:00Z")]
    public void A_day_is_due_at_the_local_time_in_the_persons_zone(string day, string time, string zone, string expected)
    {
        var now = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var due = NotificationRules.NextSendAt(zone, TimeOnly.Parse(time), [new NotificationDay(DateOnly.Parse(day), "x")], null, now);

        Assert.Equal(DateTimeOffset.Parse(expected), due);
    }

    [Fact]
    public void The_next_day_is_after_the_day_last_sent_and_not_before_today_and_has_text()
    {
        var now = new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
        var days = new[]
        {
            new NotificationDay(new DateOnly(2026, 11, 9), "yesterday"),
            new NotificationDay(new DateOnly(2026, 11, 10), "today"),
            new NotificationDay(new DateOnly(2026, 11, 11), ""),
            new NotificationDay(new DateOnly(2026, 11, 12), "later"),
        };
        var eight = new TimeOnly(8, 0);

        Assert.Equal(new DateTimeOffset(2026, 11, 10, 7, 0, 0, TimeSpan.Zero), NotificationRules.NextSendAt("Europe/Copenhagen", eight, days, null, now));
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 7, 0, 0, TimeSpan.Zero), NotificationRules.NextSendAt("Europe/Copenhagen", eight, days, new DateOnly(2026, 11, 10), now));
        Assert.Null(NotificationRules.NextSendAt("Europe/Copenhagen", eight, days, new DateOnly(2026, 11, 12), now));
    }

    [Fact]
    public void Today_is_the_day_in_the_persons_zone_and_not_in_UTC()
    {
        // 23:30 UTC on the 10th is already the 11th in Copenhagen
        var now = new DateTimeOffset(2026, 11, 10, 23, 30, 0, TimeSpan.Zero);
        var days = new[] { new NotificationDay(new DateOnly(2026, 11, 10), "ten"), new NotificationDay(new DateOnly(2026, 11, 11), "eleven") };

        var due = NotificationRules.NextSendAt("Europe/Copenhagen", new TimeOnly(8, 0), days, null, now);

        Assert.Equal(new DateTimeOffset(2026, 11, 11, 7, 0, 0, TimeSpan.Zero), due);
    }

    // The sender

    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private static readonly DateOnly SendDay = new(2026, 11, 11);

    // 08:00 in Copenhagen on the 11th
    private static readonly DateTimeOffset SendTime = new(2026, 11, 11, 7, 0, 0, TimeSpan.Zero);

    private async Task<(Guid PersonId, List<string> Endpoints)> Seed(int subscriptions, params (DateOnly Day, string Text)[] days)
    {
        var me = await SignIn();
        var endpoints = Enumerable.Range(0, subscriptions).Select(_ => NewEndpoint()).ToList();
        var list = days.Length == 0 ? [(SendDay, "Water nothing, mist the ferns."), (SendDay.AddDays(1), "Check the cuttings.")] : days;
        var notifyAt = new TimeOnly(8, 0);
        var asDays = list.Select(d => new NotificationDay(d.Day, d.Text)).ToList();

        await api.WithDbAsync(async db =>
        {
            // Other tests' schedules are not part of this run
            await db.NotificationSchedules.ExecuteDeleteAsync();
            foreach (var endpoint in endpoints)
                db.PushSubscriptions.Add(new StoredPushSubscription
                {
                    PersonId = me.PersonId, Endpoint = endpoint, EndpointHash = NotificationRules.HashEndpoint(endpoint),
                    P256dh = "BKey", Auth = "authKey", CreatedAt = SendTime,
                });
            db.NotificationSchedules.Add(new NotificationSchedule
            {
                PersonId = me.PersonId, TimeZone = "Europe/Copenhagen", NotifyAt = notifyAt, DaysJson = NotificationRules.WriteDays(asDays),
                UpdatedAt = SendTime.AddDays(-1),
                NextSendAt = NotificationRules.NextSendAt("Europe/Copenhagen", notifyAt, asDays, null, new DateTimeOffset(list.Min(d => d.Day).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)),
            });
            await db.SaveChangesAsync();
        });

        return (me.PersonId, endpoints);
    }

    private async Task RunSender(DateTimeOffset now, FakePushSender push)
    {
        await api.WithDbAsync(db =>
            new NotificationSender(db, push, new MovableClock(now), NullLogger<NotificationSender>.Instance).RunAsync(CancellationToken.None));
    }

    private async Task<NotificationSchedule> ScheduleOf(Guid personId)
    {
        NotificationSchedule? schedule = null;
        await api.WithDbAsync(async db => schedule = await db.NotificationSchedules.AsNoTracking().SingleAsync(s => s.PersonId == personId));
        return schedule!;
    }

    [Fact]
    public async Task The_text_is_sent_at_the_time_and_not_before()
    {
        var push = new FakePushSender();
        var (personId, endpoints) = await Seed(2);

        await RunSender(SendTime.AddMinutes(-1), push);
        Assert.Empty(push.Messages);

        await RunSender(SendTime, push);

        Assert.Equal(2, push.Messages.Count);
        Assert.All(endpoints, endpoint =>
        {
            var message = Assert.Single(push.To(endpoint));
            Assert.Equal(TimeSpan.FromHours(8), message.Ttl);
            var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
            Assert.Equal("Today in Stikling", payload.GetProperty("title").GetString());
            Assert.Equal("Water nothing, mist the ferns.", payload.GetProperty("body").GetString());
            Assert.Equal("today", payload.GetProperty("tag").GetString());
            Assert.Equal("/", payload.GetProperty("url").GetString());
        });
        var schedule = await ScheduleOf(personId);
        Assert.Equal(SendDay, schedule.LastSentOn);
        Assert.Equal(SendTime.AddDays(1), schedule.NextSendAt);
    }

    [Fact]
    public async Task A_day_is_sent_once_even_when_the_job_runs_twice()
    {
        var push = new FakePushSender();
        await Seed(1);

        await RunSender(SendTime.AddMinutes(5), push);
        await RunSender(SendTime.AddMinutes(20), push);

        Assert.Single(push.Messages);
    }

    [Fact]
    public async Task The_next_day_is_sent_the_next_day()
    {
        var push = new FakePushSender();
        var (_, endpoints) = await Seed(1);

        await RunSender(SendTime, push);
        await RunSender(SendTime.AddDays(1), push);

        Assert.Equal(["Water nothing, mist the ferns.", "Check the cuttings."],
            push.To(endpoints[0]).Select(m => JsonSerializer.Deserialize<JsonElement>(m.Payload).GetProperty("body").GetString()));
    }

    [Fact]
    public async Task A_day_missed_by_more_than_two_hours_is_skipped()
    {
        var push = new FakePushSender();
        var (personId, _) = await Seed(1);

        await RunSender(SendTime.AddHours(2).AddMinutes(1), push);

        Assert.Empty(push.Messages);
        var schedule = await ScheduleOf(personId);
        Assert.Equal(SendDay, schedule.LastSentOn);
        Assert.Equal(SendTime.AddDays(1), schedule.NextSendAt);
    }

    [Fact]
    public async Task A_day_missed_by_two_hours_exactly_is_still_sent()
    {
        var push = new FakePushSender();
        await Seed(1);

        await RunSender(SendTime.AddHours(2), push);

        Assert.Single(push.Messages);
    }

    [Fact]
    public async Task A_day_without_text_sends_nothing()
    {
        var push = new FakePushSender();
        var (personId, _) = await Seed(1, (SendDay, ""), (SendDay.AddDays(1), "Tomorrow"));

        await RunSender(SendTime, push);

        Assert.Empty(push.Messages);
        Assert.Equal(SendTime.AddDays(1), (await ScheduleOf(personId)).NextSendAt);
    }

    [Fact]
    public async Task When_no_days_are_left_nothing_is_due()
    {
        var push = new FakePushSender();
        var (personId, _) = await Seed(1, (SendDay, "Only"));

        await RunSender(SendTime, push);
        await RunSender(SendTime.AddDays(3), push);

        Assert.Single(push.Messages);
        Assert.Null((await ScheduleOf(personId)).NextSendAt);
    }

    [Fact]
    public async Task A_subscription_the_push_service_calls_gone_is_deleted_and_the_others_still_get_it()
    {
        var push = new FakePushSender();
        var (personId, endpoints) = await Seed(2);
        push.Answers[endpoints[0]] = PushResult.Gone;

        await RunSender(SendTime, push);

        Assert.Single(push.To(endpoints[1]));
        Assert.Equal([endpoints[1]], await EndpointsOf(personId));
    }

    [Fact]
    public async Task A_failed_send_keeps_the_subscription_and_the_rest_go_on()
    {
        var push = new FakePushSender();
        var (personId, endpoints) = await Seed(2);
        push.Answers[endpoints[0]] = PushResult.Failed;

        await RunSender(SendTime, push);

        Assert.Single(push.To(endpoints[1]));
        Assert.Equal(2, (await EndpointsOf(personId)).Count);
        Assert.Equal(SendDay, (await ScheduleOf(personId)).LastSentOn);
    }

    [Fact]
    public async Task Two_runs_with_the_same_due_value_send_once()
    {
        var push = new FakePushSender();
        await Seed(1);

        await Task.WhenAll(RunSender(SendTime, push), RunSender(SendTime, push));

        Assert.Single(push.Messages);
    }

    [Fact]
    public async Task A_day_where_every_send_failed_is_due_again_and_the_next_run_sends_it()
    {
        var push = new FakePushSender();
        var (personId, endpoints) = await Seed(2);
        push.Answers[endpoints[0]] = PushResult.Failed;
        push.Answers[endpoints[1]] = PushResult.Failed;

        await RunSender(SendTime, push);

        var schedule = await ScheduleOf(personId);
        Assert.Null(schedule.LastSentOn);
        Assert.Equal(SendTime, schedule.NextSendAt);

        push.Answers.Clear();
        await RunSender(SendTime.AddMinutes(15), push);

        Assert.Equal(4, push.Messages.Count);
        Assert.Equal(SendDay, (await ScheduleOf(personId)).LastSentOn);
    }

    [Fact]
    public async Task A_day_where_every_send_failed_is_given_up_after_two_hours()
    {
        var push = new FakePushSender();
        var (personId, endpoints) = await Seed(1);
        push.Answers[endpoints[0]] = PushResult.Failed;

        await RunSender(SendTime, push);
        push.Answers.Clear();
        await RunSender(SendTime.AddHours(2).AddMinutes(15), push);

        Assert.Single(push.Messages);
        Assert.Equal(SendDay, (await ScheduleOf(personId)).LastSentOn);
    }

    [Fact]
    public async Task One_failed_and_one_sent_moves_on()
    {
        var push = new FakePushSender();
        var (personId, endpoints) = await Seed(2);
        push.Answers[endpoints[0]] = PushResult.Failed;

        await RunSender(SendTime, push);
        await RunSender(SendTime.AddMinutes(15), push);

        Assert.Equal(2, push.Messages.Count);
        Assert.Equal(SendDay, (await ScheduleOf(personId)).LastSentOn);
    }

    [Fact]
    public async Task Turning_off_and_on_again_after_a_send_does_not_send_the_day_again()
    {
        var push = new FakePushSender();
        var (personId, _) = await Seed(1);
        await RunSender(SendTime, push);
        Assert.Single(push.Messages);

        await api.WithDbAsync(async db =>
        {
            // What PUT /me/notifications/days does with no time, then an upload that still has the day
            await db.NotificationSchedules.Where(s => s.PersonId == personId).ExecuteUpdateAsync(set => set
                .SetProperty(s => s.NextSendAt, (DateTimeOffset?)null).SetProperty(s => s.DaysJson, "[]"));
            var schedule = await db.NotificationSchedules.SingleAsync(s => s.PersonId == personId);
            var days = new List<NotificationDay> { new(SendDay, "Water nothing, mist the ferns."), new(SendDay.AddDays(1), "Check the cuttings.") };
            schedule.DaysJson = NotificationRules.WriteDays(days);
            schedule.NextSendAt = NotificationRules.NextSendAt(schedule.TimeZone, schedule.NotifyAt, days, schedule.LastSentOn, SendTime.AddMinutes(30));
            await db.SaveChangesAsync();
        });
        await RunSender(SendTime.AddMinutes(45), push);

        Assert.Single(push.Messages);
        Assert.Equal(SendTime.AddDays(1), (await ScheduleOf(personId)).NextSendAt);
    }

    [Fact]
    public async Task A_person_without_subscriptions_is_passed_over_for_the_day()
    {
        var push = new FakePushSender();
        var (personId, _) = await Seed(0);

        await RunSender(SendTime, push);

        Assert.Empty(push.Messages);
        var schedule = await ScheduleOf(personId);
        Assert.Equal(SendDay, schedule.LastSentOn);
        Assert.Equal(SendTime.AddDays(1), schedule.NextSendAt);
    }

    [Fact]
    public async Task The_time_follows_daylight_saving_from_one_day_to_the_next()
    {
        // The clocks in Copenhagen go back on 25 October 2026
        var push = new FakePushSender();
        var days = new[] { (new DateOnly(2026, 10, 24), "Saturday"), (new DateOnly(2026, 10, 25), "Sunday") };
        var (personId, _) = await Seed(1, days);
        var saturday = new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero);

        await RunSender(saturday, push);

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 7, 0, 0, TimeSpan.Zero), (await ScheduleOf(personId)).NextSendAt);
        Assert.Single(push.Messages);
    }

    [Fact]
    public async Task Everyone_who_is_due_gets_theirs()
    {
        var first = await Seed(1);
        var second = await SignIn();
        var endpoint = NewEndpoint();
        await api.WithDbAsync(async db =>
        {
            db.PushSubscriptions.Add(new StoredPushSubscription
            {
                PersonId = second.PersonId, Endpoint = endpoint, EndpointHash = NotificationRules.HashEndpoint(endpoint),
                P256dh = "BKey", Auth = "authKey", CreatedAt = SendTime,
            });
            db.NotificationSchedules.Add(new NotificationSchedule
            {
                PersonId = second.PersonId, TimeZone = "Europe/Copenhagen", NotifyAt = new TimeOnly(8, 0), UpdatedAt = SendTime,
                DaysJson = NotificationRules.WriteDays([new NotificationDay(SendDay, "Second")]), NextSendAt = SendTime,
            });
            await db.SaveChangesAsync();
        });
        var push = new FakePushSender();

        await RunSender(SendTime, push);

        Assert.Single(push.To(first.Endpoints[0]));
        Assert.Single(push.To(endpoint));
    }

    // The test message

    [Fact]
    public async Task The_test_message_goes_to_the_callers_own_endpoint()
    {
        var me = await SignIn();
        var endpoint = NewEndpoint();
        await Subscribe(me.Client, endpoint);

        var response = await me.Client.PostAsJsonAsync("/me/notifications/test", new { endpoint });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var message = Assert.Single(api.Push.To(endpoint));
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
        Assert.Equal("Stikling", payload.GetProperty("title").GetString());
        Assert.Equal("Notifications are on. Your day's list will come at the time you picked.", payload.GetProperty("body").GetString());
        Assert.Equal("test", payload.GetProperty("tag").GetString());
        Assert.Equal("/", payload.GetProperty("url").GetString());
    }

    [Fact]
    public async Task The_test_message_is_not_sent_to_someone_elses_endpoint()
    {
        var owner = await SignIn();
        var other = await SignIn();
        var endpoint = NewEndpoint();
        await Subscribe(owner.Client, endpoint);

        var response = await other.Client.PostAsJsonAsync("/me/notifications/test", new { endpoint });
        var unknown = await other.Client.PostAsJsonAsync("/me/notifications/test", new { endpoint = NewEndpoint() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Empty(api.Push.To(endpoint));
    }

    [Fact]
    public async Task A_test_to_a_gone_subscription_deletes_it()
    {
        var me = await SignIn();
        var endpoint = NewEndpoint();
        await Subscribe(me.Client, endpoint);
        api.Push.Answers[endpoint] = PushResult.Gone;

        var response = await me.Client.PostAsJsonAsync("/me/notifications/test", new { endpoint });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Empty(await EndpointsOf(me.PersonId));
    }

    // Deleting the account

    [Fact]
    public async Task Deleting_an_account_removes_its_subscriptions_and_schedule()
    {
        var me = await SignIn();
        await Subscribe(me.Client, NewEndpoint());
        await me.Client.PutAsJsonAsync("/me/notifications/days", Days("Europe/Copenhagen", "08:00", (Today(), "Today")));

        var deleted = await me.Client.DeleteAsync("/me");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await api.WithDbAsync(async db =>
        {
            Assert.False(await db.PushSubscriptions.AnyAsync(s => s.PersonId == me.PersonId));
            Assert.False(await db.NotificationSchedules.AnyAsync(s => s.PersonId == me.PersonId));
        });
    }
}
