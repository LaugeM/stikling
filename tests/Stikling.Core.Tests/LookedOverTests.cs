using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Timeline;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class LookedOverTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly FakeTimelineRepository timeline = new();
    private readonly TimelineService service;

    public LookedOverTests() => service = new TimelineService(timeline, new FakePhotoRepository(), new FixedTime(Now));

    private static TimelineEntry Entry(TimelineKind kind, TimelineEvent? @event = null, SubjectType type = SubjectType.Plant,
        DateTimeOffset? at = null, Guid? subject = null) =>
        new() { Kind = kind, Event = @event, SubjectType = type, SubjectId = subject ?? Guid.NewGuid(), OccurredAt = at ?? Now };

    [Theory]
    [InlineData(SubjectType.Plant)]
    [InlineData(SubjectType.Propagation)]
    public async Task Looking_over_writes_a_change_entry_with_the_event(SubjectType type)
    {
        var id = Guid.NewGuid();

        var entry = await service.LookedOverAsync(type, id);

        Assert.Same(entry, Assert.Single(timeline.Entries));
        Assert.Equal(TimelineKind.Change, entry.Kind);
        Assert.Equal(TimelineEvent.LookedOver, entry.Event);
        Assert.Equal("Looked over", entry.Text);
        Assert.Equal(type, entry.SubjectType);
        Assert.Equal(id, entry.SubjectId);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.False(TimelineService.CanCorrect(entry));
    }

    [Fact]
    public async Task Removing_a_look_over_soft_deletes_it()
    {
        var entry = await service.LookedOverAsync(SubjectType.Plant, Guid.NewGuid());

        await service.RemoveAsync(entry.Id);

        Assert.True(entry.IsDeleted);
        Assert.Contains(entry, timeline.Entries);
        Assert.False(TimelineActivity.Counts(entry));
    }

    [Fact]
    public void The_event_round_trips_as_text()
    {
        var json = JsonSerializer.Serialize(new TimelineEntry { Event = TimelineEvent.LookedOver }, Web);
        Assert.Contains("\"event\":\"LookedOver\"", json);

        var back = JsonSerializer.Deserialize<TimelineEntry>("""{"text":"Looked over","Event":"LookedOver"}""", Web)!;
        Assert.Equal(TimelineEvent.LookedOver, back.Event);
        Assert.Equal(TimelineEvent.LookedOver, TimelineEvents.Of(back));
    }

    [Fact]
    public void Lately_on_Today_leaves_a_look_over_out()
    {
        var subject = Guid.NewGuid();
        var looked = Entry(TimelineKind.Change, TimelineEvent.LookedOver, subject: subject);
        var note = Entry(TimelineKind.Note, subject: subject, at: Now.AddHours(-1));

        var recent = TodayBoard.RecentActivity([looked, note], new HashSet<Guid> { subject }, Now.AddDays(-30));

        Assert.Equal(note.Id, Assert.Single(recent).Id);
    }

    [Fact]
    public async Task A_look_over_is_not_a_plant_change_for_dormancy()
    {
        timeline.Entries.Add(Entry(TimelineKind.Change, TimelineEvent.LookedOver));
        var other = Entry(TimelineKind.Change);
        timeline.Entries.Add(other);

        Assert.Equal(other.Id, Assert.Single(await timeline.GetPlantChangesAsync()).Id);
    }

    [Theory]
    [InlineData(TimelineKind.Created, null, SubjectType.Plant, true)]
    [InlineData(TimelineKind.Note, null, SubjectType.Propagation, true)]
    [InlineData(TimelineKind.Photo, null, SubjectType.Plant, true)]
    [InlineData(TimelineKind.Propagated, null, SubjectType.Plant, true)]
    [InlineData(TimelineKind.Change, TimelineEvent.LookedOver, SubjectType.Plant, true)]
    [InlineData(TimelineKind.Change, TimelineEvent.LookedOver, SubjectType.Propagation, true)]
    [InlineData(TimelineKind.Change, null, SubjectType.Plant, false)]
    [InlineData(TimelineKind.Change, TimelineEvent.PottedUp, SubjectType.Propagation, false)]
    [InlineData(TimelineKind.Photo, null, SubjectType.Pot, false)]
    public void Activity_counts_the_entries_somebody_made(TimelineKind kind, TimelineEvent? @event, SubjectType type, bool counts) =>
        Assert.Equal(counts, TimelineActivity.Counts(Entry(kind, @event, type)));

    [Fact]
    public void A_deleted_entry_is_not_activity()
    {
        var entry = Entry(TimelineKind.Note);
        entry.DeletedAt = Now;

        Assert.False(TimelineActivity.Counts(entry));
    }

    [Fact]
    public async Task The_last_activity_before_a_moment_is_the_newest_that_counts()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        timeline.Entries.AddRange([
            Entry(TimelineKind.Note, subject: a, at: Now.AddDays(-5)),
            Entry(TimelineKind.Photo, subject: a, at: Now.AddDays(-2)),
            Entry(TimelineKind.Note, subject: a, at: Now),
            Entry(TimelineKind.Change, subject: a, at: Now.AddDays(-1)),
            Entry(TimelineKind.Change, TimelineEvent.LookedOver, subject: b, at: Now.AddDays(-3)),
            Entry(TimelineKind.Change, subject: Guid.NewGuid(), at: Now.AddDays(-3))
        ]);

        var last = await timeline.GetLastActivityBeforeAsync(Now);

        Assert.Equal(2, last.Count);
        Assert.Equal(Now.AddDays(-2), last[a]);
        Assert.Equal(Now.AddDays(-3), last[b]);
    }

    [Fact]
    public async Task Since_gives_the_entries_from_then_on_that_are_not_deleted()
    {
        var old = Entry(TimelineKind.Note, at: Now.AddDays(-1));
        var onTheDay = Entry(TimelineKind.Note, at: Now);
        var gone = Entry(TimelineKind.Note, at: Now.AddHours(1));
        gone.DeletedAt = Now;
        timeline.Entries.AddRange([old, onTheDay, gone]);

        var since = await timeline.GetSinceAsync(Now);

        Assert.Equal(onTheDay.Id, Assert.Single(since).Id);
    }
}
