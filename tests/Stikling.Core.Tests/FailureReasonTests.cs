using System.Text.Json;
using Stikling.Core.Models;
using Stikling.Core.Propagations;

namespace Stikling.Core.Tests;

public class FailureReasonTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 19);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly FakePlantRepository plants = new();
    private readonly FakePropagationRepository propagations = new();
    private readonly FakeTimelineRepository timeline = new();
    private readonly FixedTime time = new(Now);
    private readonly PropagationService service;

    public FailureReasonTests()
    {
        service = new PropagationService(propagations, plants, timeline, new FakePropagationCareLogRepository(), new FakeCareLogRepository(), new FakePhotoRepository(), time);
    }

    private async Task<Propagation> StartAsync(int count = 5)
    {
        var p = new Propagation { Genus = "Monstera", Type = PropagationType.Cutting, InitialCount = count, StartedOn = Today.AddDays(-30) };
        await service.CreateAsync(p, e => e.ToString());
        return p;
    }

    private TimelineEntry Failed(Propagation p) =>
        timeline.Entries.Single(e => e.SubjectId == p.Id && e.Event == TimelineEvent.Failed);

    [Fact]
    public async Task Marking_failed_writes_reason_count_and_text()
    {
        var p = await StartAsync();
        await service.MarkFailedAsync(p, 2, FailReason.Rotted);
        var entry = Failed(p);
        Assert.Equal("2 failed: rotted", entry.Text!.Split('\n')[0]);
        Assert.Equal(FailReason.Rotted, entry.FailReason);
        Assert.Equal(2, entry.Count);
    }

    [Fact]
    public async Task The_note_follows_the_reason()
    {
        var p = await StartAsync();
        await service.MarkFailedAsync(p, 2, FailReason.Rotted, " left too wet ");
        Assert.Equal("2 failed: rotted, left too wet", Failed(p).Text!.Split('\n')[0]);
    }

    [Fact]
    public async Task A_note_alone_and_nothing_alone_both_work()
    {
        var p = await StartAsync();
        await service.MarkFailedAsync(p, 1, null, "cat");
        await service.MarkFailedAsync(p, 1);
        var texts = timeline.Entries.Where(e => e.Event == TimelineEvent.Failed).Select(e => e.Text!.Split('\n')[0]).ToList();
        Assert.Contains("1 failed: cat", texts);
        Assert.Contains("1 failed", texts);
        Assert.Null(timeline.Entries.Single(e => e.Text!.StartsWith("1 failed: cat")).FailReason);
    }

    [Fact]
    public void An_entry_without_the_new_fields_still_reads()
    {
        var entry = JsonSerializer.Deserialize<TimelineEntry>("""{"kind":"Change","text":"1 failed: rotted","event":"Failed"}""", Web)!;
        Assert.Null(entry.FailReason);
        Assert.Null(entry.Count);
    }

    [Fact]
    public void A_reason_is_stored_as_text_and_an_unknown_one_reads_as_null()
    {
        var entry = new TimelineEntry { Event = TimelineEvent.Failed, FailReason = FailReason.DriedOut, Count = 3 };
        var json = JsonSerializer.Serialize(entry, Web);
        Assert.Contains("\"failReason\":\"DriedOut\"", json);
        Assert.Equal(FailReason.DriedOut, JsonSerializer.Deserialize<TimelineEntry>(json, Web)!.FailReason);

        var unknown = JsonSerializer.Deserialize<TimelineEntry>("""{"failReason":"Flooded","count":2}""", Web)!;
        Assert.Null(unknown.FailReason);
        Assert.Equal(2, unknown.Count);
    }

    private static TimelineEntry Entry(Propagation p, DateOnly day, int count, FailReason? reason) => new()
    {
        SubjectType = SubjectType.Propagation,
        SubjectId = p.Id,
        Kind = TimelineKind.Change,
        Event = TimelineEvent.Failed,
        Text = $"{count} failed",
        Count = count,
        FailReason = reason,
        OccurredAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)
    };

    private static IReadOnlyDictionary<Guid, IReadOnlyList<TimelineEntry>> By(params TimelineEntry[] entries) =>
        entries.GroupBy(e => e.SubjectId).ToDictionary(g => g.Key, g => (IReadOnlyList<TimelineEntry>)g.ToList());

    [Fact]
    public void Failures_are_counted_per_reason_per_medium_with_unrecorded_last()
    {
        var water = new Propagation { Medium = GrowingMedium.Water, StartedOn = Today.AddDays(-30), InitialCount = 10, FailedCount = 8 };
        var perlite = new Propagation { Medium = GrowingMedium.Perlite, StartedOn = Today.AddDays(-30), InitialCount = 3, FailedCount = 1 };
        var healthy = new Propagation { Medium = GrowingMedium.Leca, StartedOn = Today.AddDays(-30) };
        var entries = By(
            Entry(water, Today.AddDays(-10), 5, FailReason.Rotted),
            Entry(water, Today.AddDays(-9), 2, FailReason.DriedOut),
            Entry(water, Today.AddDays(-8), 1, null),
            Entry(perlite, Today.AddDays(-8), 1, FailReason.Mould));

        var result = FailureResults.ByMedium([water, perlite, healthy], entries, time);

        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey(GrowingMedium.Leca));
        var w = result[GrowingMedium.Water];
        Assert.Equal(8, w.Total);
        Assert.Equal([new ReasonCount(FailReason.Rotted, 5), new ReasonCount(FailReason.DriedOut, 2), new ReasonCount(null, 1)], w.Reasons);
        Assert.Equal(FailReason.Mould, result[GrowingMedium.Perlite].Reasons.Single().Reason);
    }

    [Fact]
    public void Failures_with_no_entry_count_as_not_recorded()
    {
        var p = new Propagation { Medium = GrowingMedium.Water, StartedOn = Today.AddDays(-30), InitialCount = 5, FailedCount = 4 };
        var entries = By(Entry(p, Today.AddDays(-5), 1, FailReason.Pests));

        var summary = FailureResults.ByMedium([p], entries, time)[GrowingMedium.Water];

        Assert.Equal(4, summary.Total);
        Assert.Equal(3, summary.Reasons.Single(r => r.Reason is null).Units);
    }

    [Fact]
    public void An_older_entry_without_a_count_reads_it_from_the_text()
    {
        var p = new Propagation { Medium = GrowingMedium.Water, StartedOn = Today.AddDays(-30), InitialCount = 5, FailedCount = 3 };
        var old = Entry(p, Today.AddDays(-5), 0, null);
        old.Count = null;
        old.Text = "3 failed: rotted";
        old.Event = null;

        var summary = FailureResults.ByMedium([p], By(old), time)[GrowingMedium.Water];

        Assert.Equal(3, summary.Total);
        Assert.Equal(new ReasonCount(null, 3), summary.Reasons.Single());
    }

    [Fact]
    public void Deleted_propagations_are_left_out()
    {
        var gone = new Propagation { Medium = GrowingMedium.Water, StartedOn = Today.AddDays(-30), FailedCount = 2, DeletedAt = Now };
        Assert.Empty(FailureResults.ByMedium([gone], By(Entry(gone, Today.AddDays(-1), 2, null)), time));
    }

    [Fact]
    public void The_stage_is_worked_out_from_the_dates_at_the_failure()
    {
        var p = new Propagation
        {
            StartedOn = Today.AddDays(-30),
            InitialCount = 6,
            FailedCount = 6,
            FirstRootOn = Today.AddDays(-20),
            RootedOn = Today.AddDays(-10)
        };
        var entries = By(
            Entry(p, Today.AddDays(-25), 3, FailReason.Rotted),
            Entry(p, Today.AddDays(-15), 2, FailReason.Rotted),
            Entry(p, Today.AddDays(-5), 1, FailReason.Rotted));

        var summary = FailureResults.Overall([p], entries, time)!;

        Assert.Equal(3, summary.Stages[FailureStage.BeforeRooting]);
        Assert.Equal(2, summary.Stages[FailureStage.WhileRooting]);
        Assert.Equal(1, summary.Stages[FailureStage.AfterRooting]);
        Assert.Equal(FailureStage.BeforeRooting, summary.MostlyAt);
    }

    [Fact]
    public void Seeds_use_the_first_germination_and_a_tie_has_no_mostly()
    {
        var p = new Propagation { StartedOn = Today.AddDays(-30), FirstGerminatedOn = Today.AddDays(-20) };
        Assert.Equal(FailureStage.BeforeRooting, FailureResults.StageOn(p, Today.AddDays(-21)));
        Assert.Equal(FailureStage.WhileRooting, FailureResults.StageOn(p, Today.AddDays(-20)));

        p.FailedCount = 2;
        var summary = FailureResults.Overall([p], By(Entry(p, Today.AddDays(-25), 1, null), Entry(p, Today.AddDays(-5), 1, null)), time)!;
        Assert.Null(summary.MostlyAt);
    }

    [Fact]
    public void Overall_is_null_when_nothing_failed()
    {
        Assert.Null(FailureResults.Overall([new Propagation()], By(), time));
    }

    [Fact]
    public void A_lowered_count_takes_from_not_recorded_first_then_the_oldest()
    {
        var p = new Propagation { Medium = GrowingMedium.Water, StartedOn = Today.AddDays(-30), InitialCount = 10, FailedCount = 4 };
        var entries = By(
            Entry(p, Today.AddDays(-9), 3, FailReason.Rotted),
            Entry(p, Today.AddDays(-8), 2, null),
            Entry(p, Today.AddDays(-7), 3, FailReason.DriedOut));

        var summary = FailureResults.ByMedium([p], entries, time)[GrowingMedium.Water];

        // 8 recorded, 4 left: the 2 not recorded go, then 2 of the oldest (rot)
        Assert.Equal(4, summary.Total);
        Assert.Equal([new ReasonCount(FailReason.DriedOut, 3), new ReasonCount(FailReason.Rotted, 1)], summary.Reasons);
        Assert.Equal(4, summary.Stages.Values.Sum());
    }
}
