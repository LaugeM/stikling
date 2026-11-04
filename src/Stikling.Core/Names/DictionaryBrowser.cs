namespace Stikling.Core.Names;

public sealed record GroupSummary(DictionaryGroup Group, int Genera, int Species);

public sealed record FamilySummary(string Name, DictionaryGroup Group, int Genera, int Species);

public sealed record GenusSummary(string Name, string? Family, int Species, int Cultivars, string? Everyday);

/// <param name="Name">The part after the genus, as in the file.</param>
public sealed record SpeciesSummary(string Name, string? Everyday);

public sealed record CultivarSummary(string Name, string? Species, string? Everyday);

/// <summary>An old species name and what it goes by now.</summary>
public sealed record OldName(string Old, string NowGenus, string NowSpecies);

/// <param name="Now">The genus this one is part of now, when the name is an old one.</param>
public sealed record GenusEntry(
    string Name,
    string? Family,
    DictionaryGroup Group,
    string? Now,
    string? Everyday,
    IReadOnlyList<SpeciesSummary> Species,
    IReadOnlyList<CultivarSummary> Cultivars,
    IReadOnlyList<OldName> OldNames);

/// <param name="OldNames">Full old names, like "Scindapsus aureus", of every synonym that points here.</param>
public sealed record SpeciesEntry(
    string Genus,
    string Name,
    string? Family,
    DictionaryGroup Group,
    string? Everyday,
    IReadOnlyList<CultivarSummary> Cultivars,
    IReadOnlyList<string> OldNames);

public sealed record CultivarEntry(string Genus, string Name, string? Species, string? Family, DictionaryGroup Group, string? Everyday);

/// <summary>
/// The built-in dictionary laid out for browsing by group, family, genus, species and cultivar. Built
/// once from <see cref="PlantNameData"/> and the everyday names. Old genus names are not listed in
/// groups, families or counts, but looking one up finds it with the genus it is part of now.
/// </summary>
public sealed class DictionaryBrowser
{
    public static readonly DictionaryBrowser Empty = new(PlantNameData.Empty, EverydayNameBook.Empty);

    private readonly EverydayNameBook everyday;
    private readonly Dictionary<string, GenusNames> byKey = [];
    private readonly Dictionary<DictionaryGroup, List<GenusSummary>> inGroup = [];
    private readonly Dictionary<string, List<GenusSummary>> inFamily = [];
    private readonly Dictionary<(string Genus, string Species), List<string>> oldNamesOf = [];

    public DictionaryBrowser(PlantNameData data, EverydayNameBook everyday)
    {
        this.everyday = everyday;
        foreach (var group in Enum.GetValues<DictionaryGroup>())
            inGroup[group] = [];

        var groupCounts = Enum.GetValues<DictionaryGroup>().ToDictionary(g => g, _ => (Genera: 0, Species: 0));
        var familyCounts = new Dictionary<string, (string Name, DictionaryGroup Group, int Genera, int Species)>();

        foreach (var genus in data.Genera)
        {
            byKey.TryAdd(NameKey.Of(genus.Name), genus);

            foreach (var (old, now) in genus.Synonyms)
            {
                var parts = now.Split(' ', 2);
                if (parts.Length != 2)
                    continue;
                var key = (NameKey.Of(parts[0]), NameKey.Of(parts[1]));
                if (!oldNamesOf.TryGetValue(key, out var list))
                    oldNamesOf[key] = list = [];
                list.Add($"{genus.Name} {old}");
            }

            if (genus.Now is not null)
                continue;

            var group = DictionaryGroups.Of(genus.Name, genus.Family);
            var summary = new GenusSummary(genus.Name, genus.Family, genus.Species.Count, genus.Cultivars.Count, everyday.For(genus.Name, null, null));
            inGroup[group].Add(summary);
            var counts = groupCounts[group];
            groupCounts[group] = (counts.Genera + 1, counts.Species + genus.Species.Count);

            if (!string.IsNullOrWhiteSpace(genus.Family))
            {
                var familyKey = NameKey.Of(genus.Family);
                if (!inFamily.TryGetValue(familyKey, out var members))
                    inFamily[familyKey] = members = [];
                members.Add(summary);
                var family = familyCounts.GetValueOrDefault(familyKey, (Name: genus.Family, Group: group, Genera: 0, Species: 0));
                familyCounts[familyKey] = (family.Name, family.Group, family.Genera + 1, family.Species + genus.Species.Count);
            }
        }

        foreach (var list in inGroup.Values)
            list.Sort(CompareGenera);
        foreach (var list in inFamily.Values)
            list.Sort(CompareGenera);
        foreach (var list in oldNamesOf.Values)
            list.Sort(StringComparer.OrdinalIgnoreCase);

        Groups = [.. Enum.GetValues<DictionaryGroup>().Select(g => new GroupSummary(g, groupCounts[g].Genera, groupCounts[g].Species))];
        Families = [.. familyCounts.Values
            .Select(f => new FamilySummary(f.Name, f.Group, f.Genera, f.Species))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>All nine groups in the order they are shown, including ones with nothing in them.</summary>
    public IReadOnlyList<GroupSummary> Groups { get; }

    public IReadOnlyList<FamilySummary> Families { get; }

    public IReadOnlyList<GenusSummary> GeneraIn(DictionaryGroup group) => inGroup[group];

    public IReadOnlyList<GenusSummary> GeneraInFamily(string family) =>
        inFamily.TryGetValue(NameKey.Of(family), out var list) ? list : [];

    public GenusEntry? Genus(string name)
    {
        if (!byKey.TryGetValue(NameKey.Of(name), out var genus))
            return null;

        return new GenusEntry(
            genus.Name,
            genus.Family,
            DictionaryGroups.Of(genus.Name, genus.Family),
            genus.Now,
            everyday.For(genus.Name, null, null),
            [.. genus.Species
                .OrderBy(s => NameKey.Of(s), StringComparer.Ordinal)
                .Select(s => new SpeciesSummary(s, everyday.For(genus.Name, s, null)))],
            Cultivars(genus, null),
            [.. genus.Synonyms
                .Select(s => (Old: s.Key, Parts: s.Value.Split(' ', 2)))
                .Where(s => s.Parts.Length == 2)
                .OrderBy(s => s.Old, StringComparer.OrdinalIgnoreCase)
                .Select(s => new OldName(s.Old, s.Parts[0], s.Parts[1]))]);
    }

    public SpeciesEntry? Species(string genus, string species)
    {
        if (!byKey.TryGetValue(NameKey.Of(genus), out var names))
            return null;
        var key = NameKey.Of(species);
        var found = names.Species.FirstOrDefault(s => NameKey.Of(s) == key);
        if (found is null)
            return null;

        return new SpeciesEntry(
            names.Name,
            found,
            names.Family,
            DictionaryGroups.Of(names.Name, names.Family),
            everyday.For(names.Name, found, null),
            Cultivars(names, key),
            oldNamesOf.TryGetValue((NameKey.Of(names.Name), key), out var old) ? old : []);
    }

    /// <param name="species">Picks between cultivars of the same name on different species; the first is used without it.</param>
    public CultivarEntry? Cultivar(string genus, string name, string? species = null)
    {
        if (!byKey.TryGetValue(NameKey.Of(genus), out var names))
            return null;
        var key = NameKey.Of(name);
        var same = names.Cultivars.Where(c => NameKey.Of(c.Name) == key).ToList();
        var found = same.FirstOrDefault(c => species is not null && NameKey.Of(c.Species) == NameKey.Of(species)) ?? same.FirstOrDefault();
        if (found is null)
            return null;

        return new CultivarEntry(
            names.Name,
            found.Name,
            found.Species,
            names.Family,
            DictionaryGroups.Of(names.Name, names.Family),
            everyday.For(names.Name, found.Species, found.Name));
    }

    private List<CultivarSummary> Cultivars(GenusNames genus, string? speciesKey) =>
        [.. genus.Cultivars
            .Where(c => speciesKey is null || NameKey.Of(c.Species) == speciesKey)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CultivarSummary(c.Name, c.Species, everyday.For(genus.Name, c.Species, c.Name)))];

    private static int CompareGenera(GenusSummary a, GenusSummary b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
}
