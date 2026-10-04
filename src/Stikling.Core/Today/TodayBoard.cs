using Stikling.Core.Models;

namespace Stikling.Core.Today;

/// <summary>A propagation that hasn't been looked at for a while.</summary>
public sealed record PropagationCheck(Propagation Propagation, DateOnly LastSeen, int DaysSince);

/// <summary>A plant or propagation flagged as needing something.</summary>
/// <param name="Subject">The <see cref="Plant"/> or <see cref="Propagation"/>.</param>
public sealed record AttentionItem(Entity Subject, string Name, Attention Attention);

/// <summary>Units in the propagations that finished this month, and how many of them were potted up.</summary>
public sealed record MonthResult(int PottedUp, int Total);

/// <summary>What the Today screen shows.</summary>
public static class TodayBoard
{
    /// <summary>
    /// The units in propagations that finished (Done or Failed) in the current month, and how many
    /// of them were potted up, going by <see cref="Propagation.FinishedOn"/>. Null when nothing
    /// finished this month.
    /// </summary>
    public static MonthResult? FinishedThisMonth(IEnumerable<Propagation> propagations, TimeProvider time)
    {
        var today = time.Today();
        var finished = propagations
            .Where(p => !p.IsDeleted && p.FinishedOn is { } day && day.Year == today.Year && day.Month == today.Month)
            .ToList();
        var total = finished.Sum(p => p.PottedUpCount + p.FailedCount);
        return total == 0 ? null : new MonthResult(finished.Sum(p => p.PottedUpCount), total);
    }

    /// <summary>A propagation is worth a look again after this many days without an entry.</summary>
    public const int CheckAfterDays = 7;

    /// <summary>
    /// Active propagations with nothing written down for a while, the longest wait first.
    /// A propagation with no history yet counts from the day it was started. A dormant one is
    /// left out, since there is nothing to see until it wakes up.
    /// </summary>
    public static IReadOnlyList<PropagationCheck> NeedsChecking(
        IEnumerable<Propagation> propagations,
        IReadOnlyDictionary<Guid, TimelineEntry> latestPerSubject,
        TimeProvider time,
        int afterDays = CheckAfterDays)
    {
        var today = time.Today();
        var checks = new List<PropagationCheck>();

        foreach (var propagation in propagations.Where(p => !p.IsDeleted && p.IsActive && !p.IsDormant))
        {
            var lastSeen = latestPerSubject.TryGetValue(propagation.Id, out var entry)
                ? Later(time.LocalDay(entry.OccurredAt), propagation.StartedOn)
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
    /// Plants in the collection and propagations still going that are flagged as needing
    /// something, the longest waiting first. A dormant one stays, since it was flagged on purpose.
    /// </summary>
    public static IReadOnlyList<AttentionItem> NeedsAttention(
        IEnumerable<Plant> plants, IEnumerable<Propagation> propagations) =>
        plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active && p.Attention is not null)
            .Select(p => new AttentionItem(p, p.DisplayName, p.Attention!))
            .Concat(propagations.Where(p => !p.IsDeleted && p.IsActive && p.Attention is not null)
                .Select(p => new AttentionItem(p, p.DisplayName, p.Attention!)))
            .OrderBy(i => i.Attention.Since)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Plants in the collection whose quarantine is up, from the day its length has passed. The
    /// one that has waited longest comes first.
    /// </summary>
    public static IReadOnlyList<Plant> QuarantineUp(IEnumerable<Plant> plants, DateOnly today) =>
        plants.Where(p => !p.IsDeleted && p.Status == PlantStatus.Active && p.QuarantineEnds <= today)
            .OrderBy(p => p.QuarantineEnds)
            .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>How many days back Lately on Today looks.</summary>
    public const int LatelyDays = 30;

    /// <summary>
    /// The start of the first day Lately shows, so a plant added with a date from a year back
    /// isn't listed as something recent.
    /// </summary>
    public static DateTimeOffset LatelySince(TimeProvider time) =>
        time.MomentAt(time.Today().AddDays(-LatelyDays).ToDateTime(TimeOnly.MinValue));

    /// <summary>
    /// The newest entries across the given plants and propagations, from <paramref name="since"/>
    /// on. Anything written down about a subject that has since been deleted is left out, so the
    /// list only shows what you can open.
    /// </summary>
    public static IReadOnlyList<TimelineEntry> RecentActivity(
        IEnumerable<TimelineEntry> entries, IReadOnlySet<Guid> subjectIds, DateTimeOffset since, int count = 8) =>
        Timeline.TimelineOrder.NewestFirst(entries)
            .Where(e => subjectIds.Contains(e.SubjectId) && e.OccurredAt >= since)
            .Take(count)
            .ToList();

    /// <summary>Days since the last backup, or null when there hasn't been one.</summary>
    public static int? DaysSinceBackup(DateOnly? lastBackup, DateOnly today) =>
        lastBackup is { } date ? Math.Max(0, today.DayNumber - date.DayNumber) : null;

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;
}
