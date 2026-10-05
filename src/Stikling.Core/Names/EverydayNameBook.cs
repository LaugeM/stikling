namespace Stikling.Core.Names;

/// <summary>
/// The everyday name the app shows for a plant, like "Snake plant" for Dracaena trifasciata. Looked
/// up each time rather than stored on the plant, so a better name in a later version of the app
/// shows on plants added long ago.
/// </summary>
public sealed class EverydayNameBook
{
    public static readonly EverydayNameBook Empty = new([], "en");

    private readonly Dictionary<(string Genus, string Species, string Cultivar), string> shown = [];

    /// <param name="language">The language chosen. English names fill in for plants it has no name for.</param>
    public EverydayNameBook(IEnumerable<EverydayNameData> files, string language)
    {
        Language = language;
        foreach (var file in files.OrderBy(f => f.Language == language ? 0 : f.Language == "en" ? 1 : 2))
        {
            foreach (var plant in file.Plants)
            {
                var key = (NameKey.Of(plant.Genus), NameKey.Of(plant.Species), NameKey.Of(plant.Cultivar));
                // A name that only repeats the botanical one, like "Coleus" for Coleus scutellarioides, says nothing new
                if (!shown.ContainsKey(key) && plant.Names.FirstOrDefault(n => !RepeatsBotanical(n, plant)) is { } name)
                    shown[key] = name;
            }
        }
    }

    /// <summary>"en" or "da".</summary>
    public string Language { get; }

    /// <summary>
    /// The name for a cultivar when it has one of its own, then for the species. The genus name is only
    /// used for a plant with no species, since a Hoya nobody named isn't necessarily a wax plant.
    /// </summary>
    public string? For(string? genus, string? species, string? cultivar)
    {
        var g = NameKey.Of(genus);
        if (g.Length == 0)
            return null;
        var s = NameKey.Of(species);
        var c = NameKey.Of(cultivar);

        if (c.Length > 0 && (shown.TryGetValue((g, s, c), out var name) || shown.TryGetValue((g, "", c), out name)))
            return name;
        return shown.GetValueOrDefault((g, s, ""));
    }

    private static bool RepeatsBotanical(string name, EverydayNames plant)
    {
        var key = NameKey.Of(name);
        return key == NameKey.Of(plant.Genus) || key == NameKey.Of($"{plant.Genus} {plant.Species}");
    }
}
