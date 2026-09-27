using Stikling.Core.Models;

namespace Stikling.Core.Pots;

/// <summary>
/// A pot and how many of it are taken, so the library can say what's still free before you
/// start repotting.
/// </summary>
/// <param name="InUse">Plants in the collection sitting in it.</param>
/// <param name="Gone">Pots that went with a plant given away or sold. Owned still counts them, so
/// setting the plant back to "In collection" puts the pot back without touching the pot itself.</param>
public sealed record PotUse(Pot Pot, int InUse, int Gone)
{
    public int Owned => Pot.Owned;

    /// <summary>How many of it are still in the house.</summary>
    public int Here => Math.Max(0, Owned - Gone);

    /// <summary>Never below zero: more plants than pots means none are free, not minus one.</summary>
    public int Free => Math.Max(0, Here - InUse);

    /// <summary>
    /// Every pot with the plants counted against it, sorted the way the library reads: nursery
    /// pots first, then by name, then smallest first, so the sizes of one pot line up together.
    /// A plant that died frees its pots; one given away or sold takes the ones that went with it.
    /// </summary>
    public static IReadOnlyList<PotUse> List(IEnumerable<Pot> pots, IEnumerable<Plant> plants)
    {
        var alive = plants.Where(p => !p.IsDeleted).ToList();

        return pots
            .Where(pot => !pot.IsDeleted)
            .Select(pot => new PotUse(pot, alive.Count(p => p.Uses(pot.Id)), alive.Count(p => p.TookAway(pot.Id))))
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
