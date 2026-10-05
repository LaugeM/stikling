namespace Stikling.Core.Names;

/// <summary>A name to offer while typing, and what picking it fills in.</summary>
/// <param name="Formerly">
/// The old name this replaces, e.g. "Scindapsus aureus" for Epipremnum aureum. Set when the name
/// matched is one that has changed, so picking it puts in the name it goes by now.
/// </param>
/// <param name="Everyday">
/// What people call it, e.g. "Snake plant" for Dracaena trifasciata. Only set on what the search box
/// finds: the everyday name that matched, or the one the app shows for the plant.
/// </param>
public sealed record NameSuggestion(string Genus, string? Species = null, string? Cultivar = null, string? Formerly = null, string? Everyday = null);

/// <summary>
/// The names that come with the app, laid out for looking up while typing. Built once from
/// <see cref="PlantNameData"/> and the everyday names, since that is tens of thousands of names.
/// </summary>
public sealed class PlantDictionary
{
    public static readonly PlantDictionary Empty = new(PlantNameData.Empty);

    private readonly IReadOnlyList<EverydayNameData> everyday;
    private readonly Lazy<ILookup<char, SearchEntry>> searchIndex;

    public PlantDictionary(PlantNameData data) : this(data, [], "en")
    {
    }

    /// <param name="everyday">The everyday names of each language this device uses.</param>
    /// <param name="shownLanguage">The language whose everyday names the app shows, "en" or "da".</param>
    public PlantDictionary(PlantNameData data, IReadOnlyList<EverydayNameData> everyday, string shownLanguage)
    {
        this.everyday = everyday;
        Sources = [.. data.Sources, .. everyday.SelectMany(e => e.Sources).Distinct()];
        EverydayNames = new EverydayNameBook(everyday, shownLanguage);

        var genera = new List<NameEntry>();
        var species = new List<NameEntry>();
        var cultivars = new List<NameEntry>();

        foreach (var genus in data.Genera)
        {
            genera.Add(genus.Now is { } nowGenus
                ? new NameEntry(new NameSuggestion(nowGenus, Formerly: genus.Name), genus.Name, genus.Name, null)
                : new NameEntry(new NameSuggestion(genus.Name), genus.Name, genus.Name, null));

            foreach (var name in genus.Species)
                species.Add(new NameEntry(new NameSuggestion(genus.Name, name), name, genus.Name, name));

            // An old name is listed under the genus it had, which is the one someone would type
            foreach (var (old, now) in genus.Synonyms)
            {
                var parts = now.Split(' ', 2);
                if (parts.Length == 2)
                    species.Add(new NameEntry(new NameSuggestion(parts[0], parts[1], Formerly: $"{genus.Name} {old}"), old, genus.Name, old));
            }

            foreach (var cultivar in genus.Cultivars)
                cultivars.Add(new NameEntry(new NameSuggestion(genus.Name, cultivar.Species, cultivar.Name), cultivar.Name, genus.Name, cultivar.Species));
        }

        var called = new List<NameEntry>();
        foreach (var plant in everyday.SelectMany(e => e.Plants))
        {
            foreach (var name in plant.Names)
                called.Add(new NameEntry(new NameSuggestion(plant.Genus, plant.Species, plant.Cultivar, Everyday: name), name, plant.Genus, plant.Species));
        }

        Genera = genera;
        AllSpecies = species;
        AllCultivars = cultivars;
        SpeciesByGenus = species.ToLookup(e => e.Genus, StringComparer.OrdinalIgnoreCase);
        CultivarsByGenus = cultivars.ToLookup(e => e.Genus, StringComparer.OrdinalIgnoreCase);
        EverydayByKey = called.ToLookup(e => e.Key);
        searchIndex = new(BuildSearchIndex);
    }

    /// <summary>Where the names come from, to credit them.</summary>
    public IReadOnlyList<NameSource> Sources { get; }

    /// <summary>The everyday name to show for a plant.</summary>
    public EverydayNameBook EverydayNames { get; }

    public bool IsEmpty => Genera.Count == 0;

    internal IReadOnlyList<NameEntry> Genera { get; }
    internal IReadOnlyList<NameEntry> AllSpecies { get; }
    internal IReadOnlyList<NameEntry> AllCultivars { get; }
    internal ILookup<string, NameEntry> SpeciesByGenus { get; }
    internal ILookup<string, NameEntry> CultivarsByGenus { get; }
    internal ILookup<string, NameEntry> EverydayByKey { get; }

    /// <summary>
    /// What the search box looks through, listed under the first letter of each word in it, so typing
    /// "plant" only goes through the names with a word starting with p. Built the first time it's needed.
    /// </summary>
    internal IEnumerable<SearchEntry> SearchEntriesFor(char initial) => searchIndex.Value[initial];

    private ILookup<char, SearchEntry> BuildSearchIndex()
    {
        var entries = new List<SearchEntry>();

        // The plants on the lists kept by hand, and the genera they're in
        var common = everyday.SelectMany(e => e.Plants).Where(p => p.Common).ToList();
        var commonGenera = common.Select(p => NameKey.Of(p.Genus)).ToHashSet();
        var commonSpecies = common.Where(p => p.Species is not null).Select(p => (NameKey.Of(p.Genus), NameKey.Of(p.Species))).ToHashSet();

        // A botanical name carries the everyday name it's shown with, so it says what it is
        SearchEntry Botanical(NameSuggestion name, string key)
        {
            var called = EverydayNames.For(name.Genus, name.Species, name.Cultivar);
            var isCommon = name.Species is null
                ? name.Cultivar is null && commonGenera.Contains(NameKey.Of(name.Genus))
                : commonSpecies.Contains((NameKey.Of(name.Genus), NameKey.Of(name.Species)));
            return new SearchEntry(name with { Everyday = called }, NameKey.Of(key), isCommon, Old: name.Formerly is not null, Known: called is not null);
        }

        foreach (var genus in Genera)
            entries.Add(Botanical(genus.Name, genus.Name.Formerly ?? genus.Name.Genus) with { Known = true });
        foreach (var species in AllSpecies)
            entries.Add(Botanical(species.Name, $"{species.Genus} {species.Key}"));
        foreach (var cultivar in AllCultivars)
            entries.Add(Botanical(cultivar.Name, $"{cultivar.Genus} {cultivar.Name.Species} {cultivar.Key}"));
        foreach (var plant in everyday.SelectMany(e => e.Plants))
        {
            for (var i = 0; i < plant.Names.Count; i++)
            {
                var name = new NameSuggestion(plant.Genus, plant.Species, plant.Cultivar, Everyday: plant.Names[i]);
                entries.Add(new SearchEntry(name, NameKey.Of(plant.Names[i]), plant.Common, Old: false, Known: true, Rank: i, IsEveryday: true));
            }
        }

        return entries
            .SelectMany(e => e.Key.Split(' ').Where(w => w.Length > 0).Select(w => w[0]).Distinct(), (e, initial) => (e, initial))
            .ToLookup(x => x.initial, x => x.e);
    }
}

/// <summary>
/// A name that can be suggested, with the text typing is matched against and where it's listed:
/// an old species name is listed under its old genus, a cultivar under its species.
/// </summary>
internal sealed record NameEntry(NameSuggestion Name, string Key, string Genus, string? Species)
{
    public string Key { get; } = NameKey.Of(Key);
}

/// <summary>
/// A name the search box can find. <paramref name="Key"/> is the whole name, like "monstera deliciosa"
/// or "snake plant". <paramref name="Common"/> is a plant on the lists kept by hand, or a genus with
/// one in it, which is what most people are looking for. <paramref name="Known"/> is a genus, or a plant
/// with an everyday name, which people are more likely to mean than a species nobody grows.
/// <paramref name="Rank"/> is where an everyday name comes in its plant's list, the one shown first.
/// </summary>
internal sealed record SearchEntry(NameSuggestion Name, string Key, bool Common, bool Old, bool Known, int Rank = 0, bool IsEveryday = false);

/// <summary>How names and typing are compared: case, spaces, hyphens, quotes and the hybrid sign don't count.</summary>
internal static class NameKey
{
    private static readonly char[] Ignored = ['×', '\'', '‘', '’', '"', '“', '”', '-'];

    public static string Of(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? ""
            : string.Join(' ', text.ToLowerInvariant().Split([' ', .. Ignored], StringSplitOptions.RemoveEmptyEntries));

    /// <summary>0 when the name starts with what's typed, 1 when a later word does, null when it doesn't match.</summary>
    public static int? Match(string key, string typed) =>
        key.StartsWith(typed, StringComparison.Ordinal) ? 0
        : key.Contains(' ' + typed, StringComparison.Ordinal) ? 1
        : null;
}
