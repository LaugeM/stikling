using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>A propagation that hasn't been looked at for a while.</summary>
public sealed record PropagationCheck(Propagation Propagation, DateOnly LastSeen, int DaysSince);

/// <summary>What the Today screen shows.</summary>
public static class TodayBoard
{
    /// <summary>A propagation is worth a look again after this many days without an entry.</summary>
    public const int CheckAfterDays = 7;

    /// <summary>
    /// Active propagations with nothing written down for a while, the longest wait first.
    /// A propagation with no history yet counts from the day it was started.
    /// </summary>
    public static IReadOnlyList<PropagationCheck> NeedsChecking(
        IEnumerable<Propagation> propagations,
        IReadOnlyDictionary<Guid, TimelineEntry> latestPerSubject,
        DateOnly today,
        int afterDays = CheckAfterDays)
    {
        var checks = new List<PropagationCheck>();

        foreach (var propagation in propagations.Where(p => !p.IsDeleted && p.IsActive))
        {
            var lastSeen = latestPerSubject.TryGetValue(propagation.Id, out var entry)
                ? Later(DateOnly.FromDateTime(entry.OccurredAt.UtcDateTime), propagation.StartedOn)
                : propagation.StartedOn;

            var days = today.DayNumber - lastSeen.DayNumber;
            if (days >= afterDays)
                checks.Add(new PropagationCheck(propagation, lastSeen, days));
        }

        return checks
            .OrderByDescending(c => c.DaysSince)
            .ThenBy(c => c.Propagation.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The newest entries across the given plants and propagations. Anything written down about a
    /// subject that has since been deleted is left out, so the list only shows what you can open.
    /// </summary>
    public static IReadOnlyList<TimelineEntry> RecentActivity(
        IEnumerable<TimelineEntry> entries, IReadOnlySet<Guid> subjectIds, int count = 8) =>
        Timeline.TimelineOrder.NewestFirst(entries)
            .Where(e => subjectIds.Contains(e.SubjectId))
            .Take(count)
            .ToList();

    /// <summary>Days since the last backup, or null when there hasn't been one.</summary>
    public static int? DaysSinceBackup(DateOnly? lastBackup, DateOnly today) =>
        lastBackup is { } date ? Math.Max(0, today.DayNumber - date.DayNumber) : null;

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;
}
