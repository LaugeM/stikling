using System.Text.RegularExpressions;
using Stikling.Core.Models;
using Stikling.Core.Timeline;

namespace Stikling.Core.Propagations;

/// <summary>How far a batch had come when units failed.</summary>
public enum FailureStage
{
    BeforeRooting,
    WhileRooting,
    AfterRooting
}

/// <summary>Units lost for one reason. A null reason means it wasn't recorded.</summary>
public sealed record ReasonCount(FailReason? Reason, int Units);

/// <summary>
/// What a group of propagations has lost units to, and how far along they were.
/// </summary>
/// <param name="Total">Failed units in the group.</param>
/// <param name="Reasons">Units per reason, the most first, with "not recorded" (a null reason) last.</param>
/// <param name="Stages">Units per stage, for the failures that have a date to work it out from.</param>
public sealed record FailureSummary(int Total, IReadOnlyList<ReasonCount> Reasons, IReadOnlyDictionary<FailureStage, int> Stages)
{
    /// <summary>The stage most units failed in, or null when there's no clear one.</summary>
    public FailureStage? MostlyAt
    {
        get
        {
            var ordered = Stages.Where(s => s.Value > 0).OrderByDescending(s => s.Value).ToList();
            if (ordered.Count == 0 || (ordered.Count > 1 && ordered[0].Value == ordered[1].Value))
                return null;
            return ordered[0].Key;
        }
    }
}

/// <summary>
/// Counts failed units per reason and per stage, from the failed entries on the histories.
/// Failures with no reason (older entries, or none picked) count as not recorded.
/// </summary>
public static partial class FailureResults
{
    /// <summary>The summary per medium, only for mediums that have failures.</summary>
    public static IReadOnlyDictionary<GrowingMedium, FailureSummary> ByMedium(
        IEnumerable<Propagation> propagations,
        IReadOnlyDictionary<Guid, IReadOnlyList<TimelineEntry>> entries,
        TimeProvider time) =>
        propagations
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.Medium)
            .Select(g => (g.Key, Summary: Summarise(g, entries, time)))
            .Where(g => g.Summary.Total > 0)
            .ToDictionary(g => g.Key, g => g.Summary);

    /// <summary>The summary for every propagation together, or null when nothing has failed.</summary>
    public static FailureSummary? Overall(
        IEnumerable<Propagation> propagations,
        IReadOnlyDictionary<Guid, IReadOnlyList<TimelineEntry>> entries,
        TimeProvider time)
    {
        var summary = Summarise(propagations.Where(p => !p.IsDeleted), entries, time);
        return summary.Total == 0 ? null : summary;
    }

    /// <summary>Where in the propagation's life a failure on <paramref name="day"/> fell.</summary>
    public static FailureStage StageOn(Propagation p, DateOnly day)
    {
        if (p.RootedOn is { } rooted && day >= rooted)
            return FailureStage.AfterRooting;
        var first = new[] { p.FirstRootOn, p.FirstGerminatedOn }.OfType<DateOnly>().Select(d => (DateOnly?)d).Min();
        if (first is { } f && day >= f)
            return FailureStage.WhileRooting;
        return FailureStage.BeforeRooting;
    }

    private static FailureSummary Summarise(
        IEnumerable<Propagation> propagations,
        IReadOnlyDictionary<Guid, IReadOnlyList<TimelineEntry>> entries,
        TimeProvider time)
    {
        var reasons = new Dictionary<FailReason, int>();
        var unrecorded = 0;
        var stages = new Dictionary<FailureStage, int>();
        var total = 0;

        foreach (var p in propagations)
        {
            var items = new List<(FailReason? Reason, FailureStage Stage, int Units, DateTimeOffset At)>();
            if (entries.TryGetValue(p.Id, out var list))
            {
                foreach (var entry in list.Where(e => !e.IsDeleted && TimelineEvents.Of(e) == TimelineEvent.Failed).OrderBy(e => e.OccurredAt))
                    items.Add((entry.FailReason, StageOn(p, time.LocalDay(entry.OccurredAt)), UnitsOf(entry), entry.OccurredAt));
            }

            // The count can be lowered on the edit page, so never show more than it says: take from
            // the ones with no reason first, then from the oldest
            var excess = items.Sum(i => i.Units) - Math.Max(0, p.FailedCount);
            foreach (var index in Enumerable.Range(0, items.Count).OrderBy(i => items[i].Reason is null ? 0 : 1).ThenBy(i => items[i].At))
            {
                if (excess <= 0)
                    break;
                var cut = Math.Min(excess, items[index].Units);
                items[index] = items[index] with { Units = items[index].Units - cut };
                excess -= cut;
            }

            var seen = 0;
            foreach (var (reason, stage, units, _) in items.Where(i => i.Units > 0))
            {
                seen += units;
                if (reason is { } why)
                    reasons[why] = reasons.GetValueOrDefault(why) + units;
                else
                    unrecorded += units;
                stages[stage] = stages.GetValueOrDefault(stage) + units;
            }

            // Failures with no entry left, like a corrected count, still happened
            var rest = p.FailedCount - seen;
            if (rest > 0)
            {
                seen += rest;
                unrecorded += rest;
            }
            total += seen;
        }

        return new FailureSummary(
            total,
            reasons.OrderByDescending(r => r.Value).ThenBy(r => r.Key)
                .Select(r => new ReasonCount(r.Key, r.Value))
                .Concat(unrecorded > 0 ? [new ReasonCount(null, unrecorded)] : [])
                .ToList(),
            stages);
    }

    /// <summary>Units a failed entry is about: its count, or the number its text starts with on older entries.</summary>
    private static int UnitsOf(TimelineEntry entry)
    {
        if (entry.Count is { } count)
            return Math.Max(0, count);
        return entry.Text is { } text && LeadingCount().Match(text) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 1;
    }

    [GeneratedRegex(@"^(\d+) failed")]
    private static partial Regex LeadingCount();
}
