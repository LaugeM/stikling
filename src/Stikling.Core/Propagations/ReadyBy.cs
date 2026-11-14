using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>When to start a cutting to have it rooted by a day, going by your own batches.</summary>
/// <param name="Compared">How many rooted batches of the type in the medium the answer is from.</param>
/// <param name="UsualDays">The average days to root, rounded. Null when there are too few batches.</param>
/// <param name="SlowestDays">The longest any of them took. Null when there are too few batches.</param>
/// <param name="StartBy">The day to start for the usual time to be enough. Null when there are too few batches.</param>
/// <param name="SafeStartBy">The day to start for even the slowest to be enough. Null when there are too few batches.</param>
public sealed record ReadyByResult(int Compared, int? UsualDays, int? SlowestDays, DateOnly? StartBy, DateOnly? SafeStartBy)
{
    /// <summary>There are enough batches to go by.</summary>
    public bool HasEnough => StartBy is not null;

    /// <summary>The slowest took longer than the usual, so there is a safer day to start.</summary>
    public bool HasSaferDay => SlowestDays > UsualDays;
}

/// <summary>Works out when to start for a cutting to be rooted by a date. Nothing is stored.</summary>
public static class ReadyBy
{
    public static ReadyByResult For(
        PropagationType type, GrowingMedium medium, DateOnly readyOn, IEnumerable<Propagation> all)
    {
        var days = all
            .Where(p => !p.IsDeleted && p.Type == type && p.Medium == medium)
            .Select(p => p.DaysToRoot)
            .OfType<int>()
            .ToList();
        if (days.Count < RootingPace.MinimumToCompare)
            return new ReadyByResult(days.Count, null, null, null, null);

        var usual = (int)Math.Round(days.Average(), MidpointRounding.AwayFromZero);
        var slowest = days.Max();
        return new ReadyByResult(days.Count, usual, slowest, readyOn.AddDays(-usual), readyOn.AddDays(-slowest));
    }
}
