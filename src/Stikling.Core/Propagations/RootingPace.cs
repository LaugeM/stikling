using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>
/// How a propagation that hasn't rooted yet is doing against your own earlier batches of the
/// same type in the same medium.
/// </summary>
/// <param name="DaysSoFar">Days since it was started.</param>
/// <param name="UsualDays">The average days to root of the batches it's compared with, rounded.</param>
/// <param name="Compared">How many earlier batches the average is from.</param>
public sealed record RootingPace(int DaysSoFar, int UsualDays, int Compared)
{
    /// <summary>It has gone longer than those batches took on average.</summary>
    public bool IsSlow => DaysSoFar > UsualDays;

    /// <summary>
    /// Whether a propagation is on time or slower than usual. Null when it isn't waiting to
    /// root (rooted, finished or dormant) or when fewer than <see cref="MinimumToCompare"/>
    /// batches of the same type and medium have a rooted date, since an average of one or two
    /// says more about those batches than about what's usual.
    /// </summary>
    public static RootingPace? For(Propagation propagation, IEnumerable<Propagation> all, DateOnly today)
    {
        if (propagation.IsDeleted || propagation.IsDormant || propagation.RootedOn is not null
            || propagation.Stage is not (PropagationStage.Started or PropagationStage.Rooting))
            return null;

        var days = all
            .Where(p => !p.IsDeleted && p.Id != propagation.Id
                        && p.Type == propagation.Type && p.Medium == propagation.Medium)
            .Select(p => p.DaysToRoot)
            .OfType<int>()
            .ToList();
        if (days.Count < MinimumToCompare)
            return null;

        var usual = (int)Math.Round(days.Average(), MidpointRounding.AwayFromZero);
        return new RootingPace(propagation.DaysSinceStart(today), usual, days.Count);
    }

    public const int MinimumToCompare = 3;
}
