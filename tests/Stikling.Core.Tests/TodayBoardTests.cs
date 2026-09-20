using Stikling.Core.Models;
using Stikling.Core.Today;

namespace Stikling.Core.Tests;

public class TodayBoardTests
{
    private static readonly DateOnly Today = new(2026, 9, 20);

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

        var checks = TodayBoard.NeedsChecking([quiet, fresh], new Dictionary<Guid, TimelineEntry>(), Today);

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

        Assert.Empty(TodayBoard.NeedsChecking([propagation], latest, Today));
    }

    [Fact]
    public void Finished_and_deleted_propagations_are_left_out()
    {
        var done = Started("Done", 20);
        done.RecordPottedUp(1);
        var deleted = Started("Deleted", 20);
        deleted.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Empty(TodayBoard.NeedsChecking([done, deleted], new Dictionary<Guid, TimelineEntry>(), Today));
    }

    [Fact]
    public void The_longest_wait_comes_first()
    {
        var older = Started("Older", 20);
        var newer = Started("Newer", 8);

        var checks = TodayBoard.NeedsChecking([newer, older], new Dictionary<Guid, TimelineEntry>(), Today);

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
}
