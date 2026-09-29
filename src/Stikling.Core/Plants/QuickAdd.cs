using Stikling.Core.Models;
using Stikling.Core.Names;

namespace Stikling.Core.Plants;

/// <summary>Turns a pasted or typed list of names into plants to add, one per item.</summary>
public static class QuickAdd
{
    private const int LongestName = 6;

    private static readonly char[] Separators = ['\n', '\r', ',', ';'];

    /// <summary>
    /// Splits the text on new lines, commas and semicolons. In each item the longest leading run of
    /// words the dictionary knows as a genus, species and cultivar becomes the name, spelled as the
    /// dictionary has it, and the rest goes into the notes. An item that doesn't start with a known
    /// genus becomes the nickname.
    /// </summary>
    public static IReadOnlyList<Plant> Parse(string? text, PlantDictionary dictionary, LooseDate? acquiredOn = null)
    {
        var plants = new List<Plant>();
        foreach (var item in (text ?? "").Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var words = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                continue;

            var plant = Read(words, dictionary);
            plant.AcquiredOn = acquiredOn;
            plants.Add(plant);
        }
        return plants;
    }

    private static Plant Read(string[] words, PlantDictionary dictionary)
    {
        var genus = dictionary.Genera.FirstOrDefault(g => g.Key == NameKey.Of(words[0]));
        if (genus is null)
            return new Plant { Nickname = string.Join(' ', words) };

        var used = 1;
        var plant = new Plant { Genus = genus.Name.Genus };

        var species = Longest(words, used, dictionary.SpeciesByGenus[genus.Genus]);
        if (species is not null)
        {
            plant.Genus = species.Entry.Name.Genus;
            plant.Species = species.Entry.Name.Species;
            used += species.Words;
        }

        var cultivars = dictionary.CultivarsByGenus[genus.Genus]
            .Where(c => species is null || c.Species is null || c.Species == species.Entry.Species)
            .ToList();
        if (Longest(words, used, cultivars) is { } cultivar)
        {
            plant.Cultivar = cultivar.Entry.Name.Cultivar;
            used += cultivar.Words;

            // A cultivar with no species typed says which species it is only when the dictionary has one
            if (species is null)
            {
                var same = cultivars
                    .Where(c => c.Key == cultivar.Entry.Key)
                    .Select(c => c.Name.Species)
                    .Distinct()
                    .ToList();
                plant.Species = same.Count == 1 ? same[0] : null;
            }
        }

        if (used < words.Length)
            plant.Notes = string.Join(' ', words.Skip(used));
        return plant;
    }

    private sealed record Found(NameEntry Entry, int Words);

    private static Found? Longest(string[] words, int start, IEnumerable<NameEntry> entries)
    {
        var candidates = entries.ToList();
        for (var count = Math.Min(LongestName, words.Length - start); count > 0; count--)
        {
            var key = NameKey.Of(string.Join(' ', words.Skip(start).Take(count)));
            if (key.Length > 0 && candidates.FirstOrDefault(e => e.Key == key) is { } entry)
                return new Found(entry, count);
        }
        return null;
    }
}
