namespace Stikling.Core.Models;

public static class PlantNames
{
    /// <summary>
    /// Formats a botanical name the usual way: genus capitalised, species lower case,
    /// cultivar in single quotes. Returns null when there is nothing to show.
    /// </summary>
    public static string? Botanical(string? genus, string? species, string? cultivar)
    {
        var parts = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(genus))
        {
            var g = genus.Trim();
            parts.Add(char.ToUpperInvariant(g[0]) + g[1..].ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(species))
            parts.Add(species.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(cultivar))
            parts.Add($"'{cultivar.Trim().Trim('\'', '"')}'");

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    /// <summary>Genera offered as suggestions when typing. Anything else can still be typed in.</summary>
    public static IReadOnlyList<string> CommonGenera { get; } =
    [
        "Aglaonema", "Alocasia", "Anthurium", "Asparagus", "Begonia", "Calathea", "Crassula",
        "Ctenanthe", "Dieffenbachia", "Dracaena", "Epipremnum", "Ficus", "Goeppertia", "Hoya",
        "Laurus", "Maranta", "Monstera", "Musa", "Ocimum", "Pachira", "Peperomia", "Petroselinum",
        "Phalaenopsis", "Philodendron", "Pilea", "Plectranthus", "Rhaphidophora", "Sansevieria",
        "Scindapsus", "Spathiphyllum", "Strelitzia", "Syngonium", "Tradescantia", "Zamioculcas"
    ];
}
