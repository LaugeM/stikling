using Stikling.Core.Models;

namespace Stikling.Core.Names;

public enum NameField
{
    Genus,
    Species,
    Cultivar
}

/// <summary>
/// Suggestions for the genus, species and cultivar fields while they're typed in. Names already
/// used on your plants and propagations come first, then the ones that come with the app.
/// Anything can still be typed, a suggestion only saves typing and spelling.
/// </summary>
public sealed class NameSuggester
{
    /// <summary>Species and cultivars are only looked for across every genus after this many letters, or the list is noise.</summary>
    public const int MinLettersWithoutGenus = 2;

    private readonly PlantDictionary dictionary;
    private readonly List<NameEntry> yourGenera;
    private readonly List<NameEntry> yourSpecies;
    private readonly List<NameEntry> yourCultivars;

    public NameSuggester(PlantDictionary dictionary, IEnumerable<Plant> plants, IEnumerable<Propagation> propagations)
    {
        this.dictionary = dictionary;

        // The names you use most come first when nothing is typed yet
        var used = plants.Where(p => !p.IsDeleted).Select(p => Named(p.Genus, p.Species, p.Cultivar))
            .Concat(propagations.Where(p => !p.IsDeleted).Select(p => Named(p.Genus, p.Species, p.Cultivar)))
            .Where(n => n.Genus is not null)
            .ToList();

        yourGenera = Yours(used.Select(n => new NameSuggestion(n.Genus!)), n => n.Genus, n => null);
        yourSpecies = Yours(used.Where(n => n.Species is not null).Select(n => new NameSuggestion(n.Genus!, n.Species)),
            n => n.Species!, n => n.Species);
        yourCultivars = Yours(used.Where(n => n.Cultivar is not null).Select(n => n),
            n => n.Cultivar!, n => n.Species);
    }

    public IReadOnlyList<NameSuggestion> Suggest(NameField field, string? genus, string? species, string? cultivar, int max = 6)
    {
        var typed = NameKey.Of(field switch
        {
            NameField.Genus => genus,
            NameField.Species => species,
            _ => cultivar
        });
        var inGenus = field == NameField.Genus ? null : PlantNames.Genus(genus);
        var ofSpecies = field == NameField.Cultivar ? PlantNames.Species(species) : null;

        var (yours, known) = field switch
        {
            NameField.Genus => (yourGenera, dictionary.Genera),
            NameField.Species => (yourSpecies, inGenus is null ? dictionary.AllSpecies : dictionary.SpeciesByGenus[inGenus]),
            _ => (yourCultivars, inGenus is null ? dictionary.AllCultivars : dictionary.CultivarsByGenus[inGenus])
        };

        // With nothing typed, only your own names are worth offering, except the cultivars of a genus,
        // which are few enough to pick from. Across every genus it takes a couple of letters before
        // the list says anything.
        if ((typed.Length == 0 && !(field == NameField.Cultivar && inGenus is not null))
            || (field != NameField.Genus && inGenus is null && typed.Length < MinLettersWithoutGenus))
            known = [];

        bool Fits(NameEntry e) =>
            (inGenus is null || Same(e.Genus, inGenus))
            && (ofSpecies is null || e.Species is null || Same(e.Species, ofSpecies));

        var mine = yours.Where(Fits)
            .Select(e => (Entry: e, Score: typed.Length == 0 ? 0 : NameKey.Match(e.Key, typed)))
            .Where(m => m.Score is not null)
            .OrderBy(m => m.Score);

        var others = known.Where(Fits)
            .Select(e => (Entry: e, Score: typed.Length == 0 ? 0 : NameKey.Match(e.Key, typed)))
            .Where(m => m.Score is not null)
            // Current names before old ones, then the closest match
            .OrderBy(m => m.Entry.Name.Formerly is not null)
            .ThenBy(m => m.Score);

        return mine.Concat(others)
            .Select(m => m.Entry.Name)
            .Where(s => !Unchanged(s, genus, species, cultivar))
            .DistinctBy(s => (Key(s.Genus), Key(s.Species), Key(s.Cultivar)))
            .Take(max)
            .ToList();
    }

    /// <summary>
    /// What the search box finds for what's typed: everyday names like "snake plant", and botanical
    /// names whole, like "monstera deli". Names already on your plants come first, and old names and
    /// cultivars without a name of their own come last. In between, the plants most people grow come
    /// first, and names that start with what's typed before ones where a later word does.
    /// </summary>
    public IReadOnlyList<NameSuggestion> Search(string? text, int max = 8)
    {
        var typed = NameKey.Of(text);
        if (typed.Length < MinLettersWithoutGenus)
            return [];

        var mine = yourCultivars.Concat(yourSpecies).Concat(yourGenera)
            .Select(e => (e.Name, Score: NameKey.Match(NameKey.Of(PlantNames.Botanical(e.Name.Genus, e.Name.Species, e.Name.Cultivar)), typed)))
            .Where(m => m.Score is not null)
            .OrderBy(m => m.Score)
            .Select(m => m.Name with { Everyday = dictionary.EverydayNames.For(m.Name.Genus, m.Name.Species, m.Name.Cultivar) });

        var others = dictionary.SearchEntriesFor(typed[0])
            .Select(e => (Entry: e, Score: NameKey.Match(e.Key, typed)))
            .Where(m => m.Score is not null)
            .OrderBy(m => m.Entry.Old)
            .ThenBy(m => m.Entry.Name.Cultivar is not null && !m.Entry.IsEveryday)
            .ThenByDescending(m => m.Entry.Common)
            .ThenBy(m => m.Score)
            .ThenBy(m => !m.Entry.Known)
            .ThenBy(m => m.Entry.Rank)
            .ThenBy(m => m.Entry.Key.Length)
            .ThenBy(m => !m.Entry.IsEveryday)
            .ThenBy(m => m.Entry.Key, StringComparer.Ordinal)
            .Select(m => m.Entry.Name);

        return mine.Concat(others)
            .DistinctBy(s => (Key(s.Genus), Key(s.Species), Key(s.Cultivar)))
            .Take(max)
            .ToList();
    }

    // Picking it would put in what's there already
    private static bool Unchanged(NameSuggestion s, string? genus, string? species, string? cultivar) =>
        Same(s.Genus, genus)
        && (s.Species is null || Same(s.Species, species))
        && (s.Cultivar is null || Same(s.Cultivar, cultivar));

    private static bool Same(string? a, string? b) => NameKey.Of(a) == NameKey.Of(b);

    private static string Key(string? name) => NameKey.Of(name);

    private static NameSuggestion Named(string? genus, string? species, string? cultivar) =>
        new(PlantNames.Genus(genus)!, PlantNames.Species(species), PlantNames.Cultivar(cultivar));

    /// <summary>Each name once, the most used first. The first spelling seen is kept.</summary>
    private static List<NameEntry> Yours(IEnumerable<NameSuggestion> names, Func<NameSuggestion, string> key, Func<NameSuggestion, string?> species) =>
        names
            .GroupBy(n => (Key(n.Genus), Key(n.Species), Key(n.Cultivar)))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => key(g.First()), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(n => new NameEntry(n, key(n), n.Genus, species(n)))
            .ToList();
}
