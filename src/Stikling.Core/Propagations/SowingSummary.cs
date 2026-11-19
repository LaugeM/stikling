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

    public int SeedsSown => Propagations.Sum(p => p.InitialCount);
}
