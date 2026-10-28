using Stikling.Core.Models;
using Stikling.Core.Names;

namespace Stikling.Core.Plants;

/// <summary>Looks up what a plant usually likes by its genus and species.</summary>
public sealed class PlantCareBook
{
    public static readonly PlantCareBook Empty = new(PlantCareData.Empty);

    private readonly Dictionary<string, PlantCare> byName = [];
    private readonly Dictionary<string, string> formerly = [];
    private readonly Dictionary<string, string> sources = [];

    public PlantCareBook(PlantCareData data)
    {
        foreach (var care in data.Plants)
            byName.TryAdd(NameKey.Of(care.Name), care);
        foreach (var (old, now) in data.Formerly)
            formerly.TryAdd(NameKey.Of(old), now);
        foreach (var source in data.Sources)
            sources.TryAdd(source.Host, source.Name);
    }

    public bool IsEmpty => byName.Count == 0;

    /// <summary>
    /// The line for the species when there is one, else the one for the genus. The two are never
    /// merged. An old name finds the line of the name it goes by now. The cultivar doesn't count.
    /// </summary>
    public PlantCare? Find(string? genus, string? species)
    {
        var typedGenus = NameKey.Of(genus);
        if (typedGenus.Length == 0)
            return null;

        var genusKey = typedGenus;
        var typedSpecies = NameKey.Of(species);
        if (typedSpecies.Length > 0)
        {
            var full = $"{typedGenus} {typedSpecies}";
            var resolved = formerly.TryGetValue(full, out var now) ? NameKey.Of(now) : full;
            if (byName.TryGetValue(resolved, out var own))
                return own;
            genusKey = resolved.Split(' ', 2)[0];
        }

        if (formerly.TryGetValue(genusKey, out var nowGenus))
            genusKey = NameKey.Of(nowGenus);
        return byName.GetValueOrDefault(genusKey);
    }

    /// <summary>
    /// The same, for a plant whose name may be unsure: with sp. or a lost name any stored species
    /// is ignored and the genus is looked up alone.
    /// </summary>
    public PlantCare? Find(string? genus, string? species, NameQualifier? qualifier) =>
        Find(genus, PlantNames.SpeciesToLookUp(species, qualifier));

    /// <summary>The name the app shows for a source's web address, or the host when it isn't listed.</summary>
    public string SourceName(string url)
    {
        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
        return sources.GetValueOrDefault(host, host);
    }
}
