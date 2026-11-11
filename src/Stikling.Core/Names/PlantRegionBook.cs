using Stikling.Core.Models;

namespace Stikling.Core.Names;

/// <summary>What the book knows about a plant. <paramref name="Areas"/> are Kew level 3 codes.</summary>
/// <param name="SpeciesFound">False when a species was looked up and isn't in the file.</param>
/// <param name="Genus">The genus it was found under, which is the name it goes by now when an old name was looked up.</param>
public sealed record PlantRegionInfo(string? Family, IReadOnlyList<string> Areas, bool SpeciesFound, string? Genus = null);

/// <summary>Looks up a plant's family and where it grows wild by genus and species.</summary>
public sealed class PlantRegionBook
{
    public static readonly PlantRegionBook Empty = new(PlantRegionData.Empty);

    private readonly Dictionary<string, GenusRegions> byGenus = [];
    private readonly Dictionary<string, Dictionary<string, string>> speciesByGenus = [];

    public PlantRegionBook(PlantRegionData data)
    {
        foreach (var genus in data.Genera)
        {
            var key = NameKey.Of(genus.Name);
            if (!byGenus.TryAdd(key, genus))
                continue;
            var species = new Dictionary<string, string>();
            foreach (var (name, areas) in genus.Species)
                species.TryAdd(NameKey.Of(name), areas);
            speciesByGenus[key] = species;
        }
    }

    public bool IsEmpty => byGenus.Count == 0;

    /// <summary>
    /// Null when the genus isn't in the file. An old genus name is followed one step to the genus it
    /// goes by now. A species or variety that isn't listed on its own gets the areas of its species.
    /// </summary>
    public PlantRegionInfo? Find(string? genus, string? species, NameQualifier? qualifier)
    {
        var key = NameKey.Of(genus);
        if (key.Length == 0 || !byGenus.TryGetValue(key, out var found))
            return null;

        if (found.Now is { Length: > 0 } now)
        {
            key = NameKey.Of(now);
            if (!byGenus.TryGetValue(key, out found))
                return null;
        }

        var lookUp = NameKey.Of(PlantNames.SpeciesToLookUp(species, qualifier));
        if (lookUp.Length == 0)
            return new PlantRegionInfo(found.Family, [], true, found.Name);

        var all = speciesByGenus[key];
        if (!all.TryGetValue(lookUp, out var codes))
        {
            var cut = new[] { " var. ", " subsp. ", " var ", " subsp " }
                .Select(marker => lookUp.IndexOf(marker, StringComparison.Ordinal))
                .Where(i => i > 0)
                .DefaultIfEmpty(-1)
                .Min();
            if (cut > 0)
                all.TryGetValue(lookUp[..cut], out codes);
        }

        return codes is null
            ? new PlantRegionInfo(found.Family, [], false, found.Name)
            : new PlantRegionInfo(found.Family, codes.Split(' ', StringSplitOptions.RemoveEmptyEntries), true, found.Name);
    }
}
