using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>
/// How the propagations in one medium, one type, or all of them together have done.
/// Success is counted in units, since a batch of 5 corms where 4 made it says more than
/// "1 batch". Days to root is per batch, because a batch has one rooted date.
/// </summary>
/// <param name="Batches">Propagations counted, finished or not.</param>
/// <param name="Succeeded">Units potted up, plus the units still in a batch that has rooted.</param>
/// <param name="Failed">Units marked as failed.</param>
/// <param name="Growing">Units in a batch that hasn't rooted yet, so they can still go either way.</param>
/// <param name="DaysToRoot">Days to root for each batch with a rooted date, fastest first.</param>
public sealed record PropagationResult<TKey>(
    TKey Key,
    int Batches,
    int Succeeded,
    int Failed,
    int Growing,
    IReadOnlyList<int> DaysToRoot)
{
    /// <summary>Units that have gone one way or the other.</summary>
    public int Decided => Succeeded + Failed;

    /// <summary>Share of the decided units that made it, from 0 to 1. Null while nothing is decided.</summary>
    public double? SuccessRate => Decided == 0 ? null : (double)Succeeded / Decided;

    public double? AverageDaysToRoot => DaysToRoot.Count == 0 ? null : DaysToRoot.Average();

    public int? FastestDaysToRoot => DaysToRoot.Count == 0 ? null : DaysToRoot[0];

    public int? SlowestDaysToRoot => DaysToRoot.Count == 0 ? null : DaysToRoot[^1];
}

/// <summary>
/// Success rate and days to root per medium and per type, worked out from the counts and the
/// rooted date every propagation already keeps. Deleted propagations are left out.
/// </summary>
public static class PropagationResults
{
    /// <summary>One row per medium that has been used, the most used first.</summary>
    public static IReadOnlyList<PropagationResult<GrowingMedium>> ByMedium(IEnumerable<Propagation> propagations) =>
        By(propagations, p => p.Medium);

    /// <summary>One row per type that has been used, the most used first.</summary>
    public static IReadOnlyList<PropagationResult<PropagationType>> ByType(IEnumerable<Propagation> propagations) =>
        By(propagations, p => p.Type);

    /// <summary>Everything together, or null when there are no propagations.</summary>
    public static PropagationResult<string>? Overall(IEnumerable<Propagation> propagations)
    {
        var counted = Counted(propagations).ToList();
        return counted.Count == 0 ? null : Result("All", counted);
    }

    private static List<PropagationResult<TKey>> By<TKey>(IEnumerable<Propagation> propagations, Func<Propagation, TKey> key)
        where TKey : struct, Enum =>
        Counted(propagations)
            .GroupBy(key)
            .Select(group => Result(group.Key, group.ToList()))
            .OrderByDescending(r => r.Batches)
            .ThenBy(r => r.Key)
            .ToList();

    private static IEnumerable<Propagation> Counted(IEnumerable<Propagation> propagations) =>
        propagations.Where(p => !p.IsDeleted);

    private static PropagationResult<TKey> Result<TKey>(TKey key, List<Propagation> batches) => new(
        key,
        batches.Count,
        batches.Sum(Succeeded),
        batches.Sum(p => p.FailedCount),
        batches.Sum(p => p.Stage is PropagationStage.Started or PropagationStage.Rooting ? Left(p) : 0),
        batches.Select(p => p.DaysToRoot).OfType<int>().Order().ToList());

    // Units left in a rooted batch have made it even though they haven't been potted up yet
    private static int Succeeded(Propagation p) =>
        p.PottedUpCount + (p.Stage == PropagationStage.Rooted ? Left(p) : 0);

    private static int Left(Propagation p) => Math.Max(0, p.RemainingCount);
}
