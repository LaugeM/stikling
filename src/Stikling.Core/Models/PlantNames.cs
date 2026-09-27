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

        if (Genus(genus) is { } g)
            parts.Add(g);

        if (Species(species) is { } s)
            parts.Add(s);

        if (Cultivar(cultivar) is { } c)
            parts.Add($"'{c}'");

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    /// <summary>"monstera " becomes "Monstera". Null when nothing was typed.</summary>
    public static string? Genus(string? genus)
    {
        if (string.IsNullOrWhiteSpace(genus))
            return null;
        var g = genus.Trim();
        return char.ToUpperInvariant(g[0]) + g[1..].ToLowerInvariant();
    }

    /// <summary>" Deliciosa" becomes "deliciosa". Null when nothing was typed.</summary>
    public static string? Species(string? species) =>
        string.IsNullOrWhiteSpace(species) ? null : species.Trim().ToLowerInvariant();

    /// <summary>The cultivar without the quotes around it, which are added when the name is shown.</summary>
    public static string? Cultivar(string? cultivar) =>
        string.IsNullOrWhiteSpace(cultivar) || cultivar.Trim().Trim('\'', '"') is not { Length: > 0 } c ? null : c;
}
