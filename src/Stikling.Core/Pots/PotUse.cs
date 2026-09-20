using Stikling.Core.Models;

namespace Stikling.Core.Pots;

/// <summary>
/// A pot and how many of it are taken, so the library can say what's still free before you
/// start repotting.
/// </summary>
public sealed record PotUse(Pot Pot, int InUse)
{
    public int Owned => Pot.Owned;

    /// <summary>Never below zero: more plants than pots means none are free, not minus one.</summary>
    public int Free => Math.Max(0, Owned - InUse);

    /// <summary>
    /// Every pot with the plants counted against it, sorted the way the library reads: nursery
    /// pots first, then by name, then smallest first, so the sizes of one pot line up together.
    /// </summary>
    public static IReadOnlyList<PotUse> List(IEnumerable<Pot> pots, IEnumerable<Plant> plants)
    {
        var alive = plants.Where(p => !p.IsDeleted).ToList();

        return pots
            .Where(pot => !pot.IsDeleted)
            .Select(pot => new PotUse(pot, alive.Count(p => p.InnerPotId == pot.Id || p.OuterPotId == pot.Id)))
            .OrderBy(use => use.Pot.Group)
            .ThenBy(use => use.Pot.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(use => use.Pot.TopCm ?? decimal.MaxValue)
            .ToList();
    }

    /// <summary>The names already used, for the chips on the pot form.</summary>
    public static IReadOnlyList<string> Names(IEnumerable<Pot> pots) =>
        pots.Where(pot => !pot.IsDeleted && !string.IsNullOrWhiteSpace(pot.Name))
            .Select(pot => pot.Name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
