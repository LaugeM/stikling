using Stikling.Core.Models;

namespace Stikling.Core.Pots;

/// <summary>
/// A pot and how many of it are taken, so the library can say what's still free before you
/// start repotting.
/// </summary>
/// <param name="InUse">Plants in the collection sitting in it, or for a container the propagations still going in it.</param>
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
    /// pots first, then outer pots, pots on their own and containers for propagating, then by
    /// name, then smallest first, so the sizes of one pot line up together. A plant that died
    /// frees its pots; one given away or sold takes the ones that went with it. A container counts
    /// the propagations in it that are still going (one each, however many cuttings), by its id or
    /// by the name written in Container.
    /// </summary>
    public static IReadOnlyList<PotUse> List(IEnumerable<Pot> pots, IEnumerable<Plant> plants, IEnumerable<Propagation>? propagations = null)
    {
        var alive = plants.Where(p => !p.IsDeleted && !p.LivesElsewhere).ToList();
        var potList = pots.ToList();
        var containers = new ContainerList(potList);
        var going = (propagations ?? []).Where(p => !p.IsDeleted && p.IsActive).ToList();
        var inContainer = going
            .Select(p => containers.For(p.ContainerPotId, p.Container)?.Id)
            .Where(id => id is not null)
            .GroupBy(id => id!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return potList
            .Where(pot => !pot.IsDeleted)
            .Select(pot => pot.ForPropagation
                ? new PotUse(pot, inContainer.GetValueOrDefault(pot.Id), 0)
                : new PotUse(pot, alive.Count(p => p.Uses(pot.Id)), alive.Count(p => p.TookAway(pot.Id))))
            .OrderBy(use => use.Pot.Kind)
            .ThenBy(use => use.Pot.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(use => use.Pot.TopCm ?? decimal.MaxValue)
            .ToList();
    }

    /// <summary>
    /// The containers to offer on a propagation, the one used most recently first: by the newest
    /// update of a propagation in it, then by name. Containers nothing is in come last.
    /// </summary>
    public static IReadOnlyList<PotUse> RecentContainers(IEnumerable<PotUse> uses, IEnumerable<Propagation> propagations)
    {
        var all = uses.Where(u => u.Pot.ForPropagation).ToList();
        var list = new ContainerList(all.Select(u => u.Pot));
        var latest = new Dictionary<Guid, DateTimeOffset>();
        foreach (var p in propagations.Where(p => !p.IsDeleted))
        {
            if (list.For(p.ContainerPotId, p.Container) is not { } pot)
                continue;
            if (!latest.TryGetValue(pot.Id, out var seen) || p.UpdatedAt > seen)
                latest[pot.Id] = p.UpdatedAt;
        }
        return all
            .OrderByDescending(u => latest.GetValueOrDefault(u.Pot.Id))
            .ThenBy(u => u.Pot.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The names already used for this kind of pot or container, for the chips on the pot form. None for an outer pot,
    /// since those are mostly one of a kind and a new one rarely shares a name with another.
    /// </summary>
    public static IReadOnlyList<string> Names(IEnumerable<Pot> pots, PotKind kind) =>
        kind == PotKind.Outer ? [] :
        pots.Where(pot => !pot.IsDeleted && pot.Kind == kind && !string.IsNullOrWhiteSpace(pot.Name))
            .Select(pot => pot.Name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
