using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Plants;
using Stikling.Core.Propagations;
using Stikling.Core.Timeline;

namespace Stikling.Core.Tests;

public class TimelineEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 19);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly PropagationService service;
    private readonly FixedTime time = new(Now);

    public TimelineEventTests()
    {
        service = new PropagationService(propagations, plants, timeline, time);
    }

    private async Task<Propagation> StartAsync(int count, DateOnly startedOn)
    {
        var corms = new Propagation { Genus = "Alocasia", Type = PropagationType.Corm, InitialCount = count, StartedOn = startedOn };
        await service.CreateAsync(corms, e => e.ToString());
        return corms;
    }

    private TimelineEntry Change(Propagation p) =>
        timeline.Entries.Single(e => e.SubjectId == p.Id && e.Kind == TimelineKind.Change);

    [Fact]
    public async Task Mark_failed_on_a_past_day_uses_that_day()
    {
        var corms = await StartAsync(1, Today.AddDays(-20));
        var day = Today.AddDays(-7);

        await service.MarkFailedAsync(corms, 1, "rotted", day);

        Assert.Equal(day, corms.FinishedOn);
        var entry = Change(corms);
        Assert.Equal(time.MomentOn(day), entry.OccurredAt);
        Assert.Equal(TimelineEvent.Failed, entry.Event);
    }

    [Fact]
    public async Task Mark_failed_without_a_date_uses_today()
    {
        var corms = await StartAsync(1, Today.AddDays(-2));
        await service.MarkFailedAsync(corms, 1);
        Assert.Equal(Today, corms.FinishedOn);
    }

    [Fact]
    public async Task Mark_failed_refuses_a_future_day_or_one_before_the_start()
    {
        var corms = await StartAsync(2, Today.AddDays(-5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MarkFailedAsync(corms, 1, null, Today.AddDays(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MarkFailedAsync(corms, 1, null, Today.AddDays(-6)));

        Assert.Equal(0, corms.FailedCount);
        Assert.DoesNotContain(timeline.Entries, e => e.Kind == TimelineKind.Change);
    }

    [Fact]
    public async Task Pot_up_sets_the_event_and_finishes_on_the_given_day()
    {
        var corms = await StartAsync(1, Today.AddDays(-20));
        var day = Today.AddDays(-3);

        await service.PotUpAsync(corms, new PotUpRequest(1, day));

        Assert.Equal(TimelineEvent.PottedUp, Change(corms).Event);
        Assert.Equal(day, corms.FinishedOn);
    }

    [Theory]
    [InlineData("\"PottedUp\"", TimelineEvent.PottedUp)]
    [InlineData("\"Failed\"", TimelineEvent.Failed)]
    [InlineData("\"Replanted\"", null)]
    [InlineData("7", null)]
    [InlineData("null", null)]
    public void The_event_reads_unknown_values_as_null(string value, TimelineEvent? expected)
    {
        var entry = JsonSerializer.Deserialize<TimelineEntry>($$"""{"text":"x","event":{{value}}}""", Web)!;
        Assert.Equal(expected, entry.Event);
        Assert.Equal("x", entry.Text);
    }

    [Fact]
    public void An_entry_without_the_property_has_no_event()
    {
        var entry = JsonSerializer.Deserialize<TimelineEntry>("""{"text":"x"}""", Web)!;
        Assert.Null(entry.Event);
    }

    [Fact]
    public void The_event_is_written_as_text()
    {
        var json = JsonSerializer.Serialize(new TimelineEntry { Event = TimelineEvent.Failed }, Web);
        Assert.Contains("\"event\":\"Failed\"", json);
    }

    private static TimelineEntry Old(string text, SubjectType subject = SubjectType.Propagation, TimelineKind kind = TimelineKind.Change) =>
        new() { SubjectType = subject, Kind = kind, Text = text };

    [Theory]
    [InlineData("Potted up 1: Alocasia", TimelineEvent.PottedUp)]
    [InlineData("Potted up 12: A, B\nFinished: 12 potted up", TimelineEvent.PottedUp)]
    [InlineData("1 failed", TimelineEvent.Failed)]
    [InlineData("3 failed: rotted", TimelineEvent.Failed)]
    [InlineData("2 failed: dried out\nFinished: 1 potted up, 2 failed", TimelineEvent.Failed)]
    [InlineData("Potted up the best ones", null)]
    [InlineData("Potted up: all", null)]
    [InlineData("failed", null)]
    [InlineData("Count: 4 (was 3)\n1 failed", null)]
    public void Old_entries_are_recognised_from_their_text(string text, TimelineEvent? expected) =>
        Assert.Equal(expected, TimelineEvents.Of(Old(text)));

    [Fact]
    public void Text_is_only_recognised_on_change_entries_of_propagations()
    {
        Assert.Null(TimelineEvents.Of(Old("1 failed", SubjectType.Plant)));
        Assert.Null(TimelineEvents.Of(Old("1 failed", kind: TimelineKind.Note)));
    }

    [Fact]
    public void The_field_wins_over_the_text()
    {
        var entry = Old("Potted up 1: x");
        entry.Event = TimelineEvent.Failed;
        Assert.Equal(TimelineEvent.Failed, TimelineEvents.Of(entry));
    }
}
