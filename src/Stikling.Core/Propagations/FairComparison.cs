using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Propagations;

/// <summary>Something batches can differ in besides how they turned out.</summary>
public enum ComparedThing
{
    Medium,
    Type,
    Plant,
    StartMonth,
    RootingAids,
    Drying,
    Place
}

/// <summary>
/// Whether batches being compared differ in more than the one thing the comparison is about, so a
/// difference in results isn't read as that one thing's doing. It only says what differs, from what
/// the batches have recorded. A plant name or place left empty doesn't count as different, and a place
/// merged into another counts as the one it was merged into.
/// </summary>
public static class FairComparison
{
    /// <summary>Batches started closer together than this count as started at the same time, even across a month's end.</summary>
    private const int SameTimeDays = 14;

    /// <summary>
    /// What differs between the batches of an experiment. A list of two or more means it isn't
    /// comparing one thing at a time. See <see cref="IsMoreThanOne"/>.
    /// </summary>
    public static IReadOnlyList<ComparedThing> InExperiment(ExperimentGroup group, Places places)
    {
        var batches = group.Entries.Select(e => e.Propagation).Where(p => !p.IsDeleted).ToList();
        if (batches.Count < 2)
            return [];

        var differing = new List<ComparedThing>();
        if (Varies(batches, p => p.Medium.ToString()))
            differing.Add(ComparedThing.Medium);
        if (Varies(batches, p => p.Type.ToString()))
            differing.Add(ComparedThing.Type);
        if (PlantVaries(batches))
            differing.Add(ComparedThing.Plant);
        if (StartVaries(batches))
            differing.Add(ComparedThing.StartMonth);
        if (Varies(batches, AidsKey))
            differing.Add(ComparedThing.RootingAids);
        if (Varies(batches, DryingKey))
            differing.Add(ComparedThing.Drying);
        if (Varies(batches, p => PlaceKey(p, places)))
            differing.Add(ComparedThing.Place);
        return differing;
    }

    public static bool IsMoreThanOne(IReadOnlyList<ComparedThing> differing) => differing.Count >= 2;

    /// <summary>What the mediums on the results page mostly differ in, besides the medium.</summary>
    public static IReadOnlyList<ComparedThing> AcrossMediums(IEnumerable<Propagation> propagations, Places places) =>
        Between(Live(propagations).GroupBy(p => p.Medium).Select(g => g.ToList()).ToList(), ComparedThing.Medium, places);

    /// <summary>What the batches with a rooting aid and the batches without it mostly differ in, over every aid in use.</summary>
    public static IReadOnlyList<ComparedThing> AcrossRootingAids(IEnumerable<Propagation> propagations, Places places)
    {
        var live = Live(propagations).ToList();
        var differing = Enum.GetValues<RootingAid>()
            .Where(aid => live.Any(p => p.RootingAids.Contains(aid)))
            .SelectMany(aid => Between(
                [
                    live.Where(p => p.RootingAids.Contains(aid)).ToList(),
                    live.Where(p => !p.RootingAids.Contains(aid)).ToList()
                ],
                ComparedThing.RootingAids,
                places))
            .ToHashSet();
        return [.. Enum.GetValues<ComparedThing>().Where(differing.Contains)];
    }

    /// <summary>What the groups of batches by how long they dried mostly differ in, besides the drying.</summary>
    public static IReadOnlyList<ComparedThing> AcrossDrying(IEnumerable<Propagation> propagations, Places places) =>
        Between(Live(propagations).GroupBy(DryingKey).Select(g => g.ToList()).ToList(), ComparedThing.Drying, places);

    /// <summary>
    /// The things where what is most common in one group is not what is most common in another. A whole
    /// collection is never alike down to the last batch, so a single batch out of line doesn't count.
    /// </summary>
    private static List<ComparedThing> Between(IReadOnlyList<List<Propagation>> groups, ComparedThing about, Places places)
    {
        var filled = groups.Where(g => g.Count > 0).ToList();
        if (filled.Count < 2)
            return [];

        var differing = new List<ComparedThing>();
        foreach (var thing in Enum.GetValues<ComparedThing>().Where(t => t != about))
        {
            var differs = thing == ComparedThing.StartMonth
                ? MostCommonDiffer(filled, p => p.StartedOn.Month.ToString(), (a, b) => MonthsApart(int.Parse(a), int.Parse(b)) <= 1)
                : MostCommonDiffer(filled, KeyFor(thing, places), (a, b) => a == b);
            if (differs)
                differing.Add(thing);
        }
        return differing;
    }

    private static Func<Propagation, string?> KeyFor(ComparedThing thing, Places places) => thing switch
    {
        ComparedThing.Medium => p => p.Medium.ToString(),
        ComparedThing.Type => p => p.Type.ToString(),
        ComparedThing.Plant => GenusKey,
        ComparedThing.RootingAids => AidsKey,
        ComparedThing.Drying => DryingKey,
        ComparedThing.Place => p => PlaceKey(p, places),
        _ => throw new ArgumentOutOfRangeException(nameof(thing), thing, null)
    };

    /// <summary>True when two groups have most common values with nothing in common. Values that tie all count.</summary>
    private static bool MostCommonDiffer(List<List<Propagation>> groups, Func<Propagation, string?> key, Func<string, string, bool> alike)
    {
        var tops = new List<List<string>>();
        foreach (var group in groups)
        {
            var counts = group.Select(key).OfType<string>().GroupBy(v => v).Select(v => (v.Key, Count: v.Count())).ToList();
            if (counts.Count == 0)
                continue;
            var most = counts.Max(c => c.Count);
            tops.Add(counts.Where(c => c.Count == most).Select(c => c.Key).ToList());
        }

        for (var i = 0; i < tops.Count; i++)
            for (var j = i + 1; j < tops.Count; j++)
                if (!tops[i].Any(a => tops[j].Any(b => alike(a, b))))
                    return true;
        return false;
    }

    /// <summary>How far apart two months of the year are, going the short way round, so December and January are 1.</summary>
    internal static int MonthsApart(int a, int b)
    {
        var gap = Math.Abs(a - b);
        return Math.Min(gap, 12 - gap);
    }

    private static IEnumerable<Propagation> Live(IEnumerable<Propagation> propagations) =>
        propagations.Where(p => !p.IsDeleted);

    private static bool Varies(List<Propagation> batches, Func<Propagation, string?> key) =>
        batches.Select(key).OfType<string>().Distinct().Skip(1).Any();

    /// <summary>Different genera, or different species of a genus. "Monstera" next to "Monstera deliciosa" isn't known to differ.</summary>
    private static bool PlantVaries(List<Propagation> batches)
    {
        if (Varies(batches, GenusKey))
            return true;
        return batches.Where(p => GenusKey(p) is not null).GroupBy(GenusKey).Any(g => Varies(g.ToList(), SpeciesKey));
    }

    private static bool StartVaries(List<Propagation> batches)
    {
        var first = batches.Min(p => p.StartedOn);
        var last = batches.Max(p => p.StartedOn);
        var otherMonth = first.Year != last.Year || first.Month != last.Month;
        return otherMonth && last.DayNumber - first.DayNumber > SameTimeDays;
    }

    private static string? GenusKey(Propagation p) =>
        string.IsNullOrWhiteSpace(p.Genus) ? null : p.Genus.Trim().ToLowerInvariant();

    private static string? SpeciesKey(Propagation p) =>
        string.IsNullOrWhiteSpace(p.KnownSpecies) ? null : p.KnownSpecies.Trim().ToLowerInvariant();

    private static string AidsKey(Propagation p) =>
        string.Join(',', p.RootingAids.Distinct().Order().Select(a => a.ToString()));

    // The same three groups as the results page: a few hours more or less isn't a different setup
    private static string DryingKey(Propagation p) => p.DriedForHours switch
    {
        null or <= 0 => "none",
        < 24 => "under a day",
        _ => "a day or more"
    };

    private static string? PlaceKey(Propagation p, Places places) => places.Find(p.PlaceId)?.Id.ToString();
}
