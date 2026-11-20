using Stikling.Core.Models;

namespace Stikling.Core.Propagations;

/// <summary>What has been sown from a seed lot: the seed propagations that point to it.</summary>
public sealed record SowingSummary(IReadOnlyList<Propagation> Propagations)
{
    public static readonly SowingSummary None = new([]);

    public int Count => Propagations.Count;

    /// <summary>The first day anything was sown from the lot.</summary>
    public DateOnly? FirstSownOn => Propagations.Count == 0 ? null : Propagations.Min(p => p.StartedOn);

    /// <summary>The latest day anything was sown from the lot.</summary>
    public DateOnly? LastSownOn => Propagations.Count == 0 ? null : Propagations.Max(p => p.StartedOn);

    /// <summary>
    /// The seeds sown. A propagation split off another one here holds seeds already counted on the one
    /// it came from, so it adds none.
    /// </summary>
    public int SeedsSown
    {
        get
        {
            var ids = Propagations.Select(p => p.Id).ToHashSet();
            return Propagations.Where(p => !IsSplitOfAnother(p, ids)).Sum(p => p.InitialCount);
        }
    }

    /// <summary>True when the propagation was split off one of <paramref name="ids"/>, so its seeds are counted there.</summary>
    public static bool IsSplitOfAnother(Propagation propagation, IReadOnlySet<Guid> ids) =>
        propagation.SplitFromId is { } from && ids.Contains(from);
}
