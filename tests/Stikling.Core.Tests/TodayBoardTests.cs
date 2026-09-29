using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class TodayBoardTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);
    private static readonly FixedTime Time = new(new DateTimeOffset(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

    private static Propagation Finished(int pottedUp, int failed, int daysAgo, bool deleted = false)
    {
        var propagation = new Propagation
        {
            InitialCount = pottedUp + failed,
            PottedUpCount = pottedUp,
            FailedCount = failed,
            DeletedAt = deleted ? Time.GetUtcNow() : null
        };
        propagation.SyncStageWithCounts(Today.AddDays(-daysAgo));
        return propagation;
    }

    [Fact]
    public void Finished_this_month_counts_units_potted_up_out_of_all_that_finished()
    {
        var result = TodayBoard.FinishedThisMonth(
            [Finished(2, 1, 3), Finished(1, 0, 10), Finished(5, 5, 40), Finished(0, 2, 1, deleted: true)], Time);

        Assert.Equal(new MonthResult(3, 4), result);
    }

    [Fact]
    public void Finished_this_month_is_null_when_nothing_finished()
    {
        var going = new Propagation { InitialCount = 3 };

        Assert.Null(TodayBoard.FinishedThisMonth([going, Finished(1, 0, 40)], Time));
    }

    private static Propagation Started(string name, int daysAgo) => new()
    {
        Nickname = name,
        StartedOn = Today.AddDays(-daysAgo)
    };

    private static TimelineEntry Entry(Guid subjectId, int daysAgo) => new()
    {
        SubjectType = SubjectType.Propagation,
        SubjectId = subjectId,
        Kind = TimelineKind.Note,
        OccurredAt = new DateTimeOffset(Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)
    };

    [Fact]
    public void Propagations_with_nothing_written_down_for_a_week_need_a_look()
    {
        var quiet = Started("Corms", 9);
        var fresh = Started("Coleus", 2);

        var checks = TodayBoard.NeedsChecking([quiet, fresh], new Dictionary<Guid, TimelineEntry>(), Time);

        var check = Assert.Single(checks);
        Assert.Same(quiet, check.Propagation);
        Assert.Equal(9, check.DaysSince);
        Assert.Equal(Today.AddDays(-9), check.LastSeen);
    }

    [Fact]
    public void A_recent_note_counts_as_looking_at_it()
    {
        var propagation = Started("Corms", 30);
        var latest = new Dictionary<Guid, TimelineEntry> { [propagation.Id] = Entry(propagation.Id, 3) };

        Assert.Empty(TodayBoard.NeedsChecking([propagation], latest, Time));
    }

    [Fact]
    public void A_note_written_just_after_midnight_counts_for_that_day()
    {
        // 00:30 on the 20th in Denmark in summer is still the 19th in UTC
        var danish = new FixedTime(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(2));
        var propagation = Started("Corms", 30);
        var note = Entry(propagation.Id, 0);
        note.OccurredAt = new DateTimeOffset(2026, 9, 20, 0, 30, 0, TimeSpan.FromHours(2));
        var latest = new Dictionary<Guid, TimelineEntry> { [propagation.Id] = note };

        var check = Assert.Single(TodayBoard.NeedsChecking([propagation], latest, danish));

        Assert.Equal(Today, check.LastSeen);
        Assert.Equal(7, check.DaysSince);
    }

    [Fact]
    public void Finished_and_deleted_propagations_are_left_out()
    {
        var done = Started("Done", 20);
        done.RecordPottedUp(1, new DateOnly(2026, 9, 20));
        var deleted = Started("Deleted", 20);
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(TodayBoard.NeedsChecking([done, deleted], new Dictionary<Guid, TimelineEntry>(), Time));
    }

    [Fact]
    public void The_longest_wait_comes_first()
    {
        var older = Started("Older", 20);
        var newer = Started("Newer", 8);

        var checks = TodayBoard.NeedsChecking([newer, older], new Dictionary<Guid, TimelineEntry>(), Time);

        Assert.Equal(["Older", "Newer"], checks.Select(c => c.Propagation.DisplayName));
    }

    [Fact]
    public void Recent_activity_is_newest_first_and_limited()
    {
        var entries = Enumerable.Range(1, 12).Select(i => Entry(Guid.NewGuid(), i)).ToList();
        var subjects = entries.Select(e => e.SubjectId).ToHashSet();

        var recent = TodayBoard.RecentActivity(entries, subjects, 5);

        Assert.Equal(5, recent.Count);
        Assert.Equal(entries[0].Id, recent[0].Id);
    }

    [Fact]
    public void Recent_activity_leaves_out_entries_from_deleted_subjects()
    {
        var kept = Guid.NewGuid();
        var gone = Guid.NewGuid();
        var entries = new List<TimelineEntry> { Entry(gone, 1), Entry(kept, 2), Entry(gone, 3) };

        var recent = TodayBoard.RecentActivity(entries, new HashSet<Guid> { kept });

        var entry = Assert.Single(recent);
        Assert.Equal(kept, entry.SubjectId);
    }

    [Fact]
    public void Recent_activity_fills_up_from_what_is_left()
    {
        var kept = Guid.NewGuid();
        var gone = Guid.NewGuid();
        var entries = Enumerable.Range(1, 12)
            .Select(i => Entry(i <= 4 ? gone : kept, i))
            .ToList();

        var recent = TodayBoard.RecentActivity(entries, new HashSet<Guid> { kept }, 5);

        Assert.Equal(5, recent.Count);
        Assert.All(recent, e => Assert.Equal(kept, e.SubjectId));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    [InlineData(45, 45)]
    public void Days_since_the_last_backup(int? daysAgo, int? expected)
    {
        var last = daysAgo is { } d ? Today.AddDays(-d) : (DateOnly?)null;

        Assert.Equal(expected, TodayBoard.DaysSinceBackup(last, Today));
    }

    private static Plant Quarantined(string name, int daysAgo, int? length = null) => new()
    {
        Nickname = name,
        QuarantinedSince = Today.AddDays(-daysAgo),
        QuarantineDays = length
    };

    [Fact]
    public void Quarantine_is_not_up_before_its_length_has_passed()
    {
        Assert.Empty(TodayBoard.QuarantineUp([Quarantined("New alocasia", 13)], Today));
    }

    [Fact]
    public void Quarantine_is_up_on_the_day_its_length_is_reached()
    {
        var plant = Quarantined("New alocasia", 14);

        Assert.Equal([plant], TodayBoard.QuarantineUp([plant], Today));
    }

    [Fact]
    public void Quarantine_uses_the_length_set_on_the_plant()
    {
        var short_ = Quarantined("Short", 7, length: 7);
        var long_ = Quarantined("Long", 20, length: 30);

        Assert.Equal([short_], TodayBoard.QuarantineUp([short_, long_], Today));
    }

    [Fact]
    public void An_overdue_quarantine_is_listed_and_the_longest_overdue_comes_first()
    {
        var recent = Quarantined("Recent", 15);
        var old = Quarantined("Old", 30);

        Assert.Equal([old, recent], TodayBoard.QuarantineUp([recent, old], Today));
    }

    [Fact]
    public void Plants_not_in_quarantine_deleted_or_gone_are_left_out()
    {
        var free = new Plant { Nickname = "Free" };
        var deleted = Quarantined("Deleted", 20);
        deleted.DeletedAt = DateTimeOffset.UnixEpoch;
        var gone = Quarantined("Gone", 20);
        gone.Status = PlantStatus.Died;

        Assert.Empty(TodayBoard.QuarantineUp([free, deleted, gone], Today));
    }
}
