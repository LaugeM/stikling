namespace Stikling.Core.Names;

/// <summary>The groups of plants people know, which the dictionary is browsed by. Not stored anywhere.</summary>
public enum DictionaryGroup
{
    Aroids,
    PrayerPlants,
    Begonias,
    Carnivorous,
    Cacti,
    Succulents,
    Orchids,
    Bromeliads,
    Other
}

/// <summary>Which group a genus belongs to, by the genus itself first and then by its family.</summary>
public static class DictionaryGroups
{
    private static readonly Dictionary<string, DictionaryGroup> ByGenus = Build(
        (DictionaryGroup.Carnivorous, ["Pinguicula", "Utricularia", "Genlisea", "Cephalotus", "Drosophyllum", "Byblis", "Roridula", "Darlingtonia", "Heliamphora", "Sarracenia", "Dionaea", "Drosera", "Aldrovanda", "Nepenthes", "Triphyophyllum"]),
        (DictionaryGroup.Succulents, ["Aloe", "Aloiampelos", "Aristaloe", "Gonialoe", "Kumara", "Haworthia", "Haworthiopsis", "Tulista", "Gasteria", "Astroloba"]));

    private static readonly Dictionary<string, DictionaryGroup> ByFamily = Build(
        (DictionaryGroup.Aroids, ["Araceae"]),
        (DictionaryGroup.PrayerPlants, ["Marantaceae"]),
        (DictionaryGroup.Begonias, ["Begoniaceae"]),
        (DictionaryGroup.Carnivorous, ["Nepenthaceae", "Droseraceae", "Sarraceniaceae", "Lentibulariaceae", "Cephalotaceae", "Drosophyllaceae", "Byblidaceae", "Roridulaceae"]),
        (DictionaryGroup.Cacti, ["Cactaceae"]),
        (DictionaryGroup.Succulents, ["Crassulaceae", "Aizoaceae"]),
        (DictionaryGroup.Orchids, ["Orchidaceae"]),
        (DictionaryGroup.Bromeliads, ["Bromeliaceae"]));

    public static DictionaryGroup Of(string genus, string? family)
    {
        if (ByGenus.TryGetValue(NameKey.Of(genus), out var group))
            return group;
        return ByFamily.TryGetValue(NameKey.Of(family), out group) ? group : DictionaryGroup.Other;
    }

    private static Dictionary<string, DictionaryGroup> Build(params (DictionaryGroup Group, string[] Names)[] lists) =>
        lists.SelectMany(l => l.Names.Select(n => (Key: NameKey.Of(n), l.Group))).ToDictionary(x => x.Key, x => x.Group);
}
