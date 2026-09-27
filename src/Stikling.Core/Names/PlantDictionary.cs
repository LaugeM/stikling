namespace Stikling.Core.Names;

/// <summary>A name to offer while typing, and what picking it fills in.</summary>
/// <param name="Formerly">
/// The old name this replaces, e.g. "Scindapsus aureus" for Epipremnum aureum. Set when the name
/// matched is one that has changed, so picking it puts in the name it goes by now.
/// </param>
public sealed record NameSuggestion(string Genus, string? Species = null, string? Cultivar = null, string? Formerly = null);

/// <summary>
/// The names that come with the app, laid out for looking up while typing. Built once from
/// <see cref="PlantNameData"/>, since that is a few thousand names.
/// </summary>
public sealed class PlantDictionary
{
    public static readonly PlantDictionary Empty = new(PlantNameData.Empty);

    public PlantDictionary(PlantNameData data)
    {
        Sources = data.Sources;

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

        Genera = genera;
        AllSpecies = species;
        AllCultivars = cultivars;
        SpeciesByGenus = species.ToLookup(e => e.Genus, StringComparer.OrdinalIgnoreCase);
        CultivarsByGenus = cultivars.ToLookup(e => e.Genus, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Where the names come from, to credit them.</summary>
    public IReadOnlyList<NameSource> Sources { get; }

    public bool IsEmpty => Genera.Count == 0;

    internal IReadOnlyList<NameEntry> Genera { get; }
    internal IReadOnlyList<NameEntry> AllSpecies { get; }
    internal IReadOnlyList<NameEntry> AllCultivars { get; }
    internal ILookup<string, NameEntry> SpeciesByGenus { get; }
    internal ILookup<string, NameEntry> CultivarsByGenus { get; }
}

/// <summary>
/// A name that can be suggested, with the text typing is matched against and where it's listed:
/// an old species name is listed under its old genus, a cultivar under its species.
/// </summary>
internal sealed record NameEntry(NameSuggestion Name, string Key, string Genus, string? Species)
{
    public string Key { get; } = NameKey.Of(Key);
}

/// <summary>How names and typing are compared: case, spaces, quotes and the hybrid sign don't count.</summary>
internal static class NameKey
{
    private static readonly char[] Ignored = ['×', '\'', '‘', '’', '"', '“', '”'];

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
