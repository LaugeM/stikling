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

    /// <summary>
    /// The name with how sure it is: "Hoya cf. carnosa 'Krimson Queen'", "Hoya sp. Sumatra EPC 123".
    /// With sp. the species is left out. The note goes last. Null when there is nothing to show.
    /// </summary>
    public static string? Botanical(string? genus, string? species, string? cultivar, NameQualifier? qualifier, string? note)
    {
        var g = Genus(genus);
        var s = qualifier == NameQualifier.Sp ? null : Species(species);
        var parts = new List<string>(5);
        if (g is not null)
            parts.Add(g);
        if (qualifier == NameQualifier.Cf && s is not null)
            parts.Add("cf.");
        else if (qualifier == NameQualifier.Aff && s is not null)
            parts.Add("aff.");
        else if (qualifier == NameQualifier.Sp && g is not null)
            parts.Add("sp.");
        if (s is not null)
            parts.Add(s);
        if (Cultivar(cultivar) is { } c)
            parts.Add($"'{c}'");
        if (Note(note) is { } n)
            parts.Add(n);
        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    /// <summary>
    /// True when a name that was unsure has been found out: the qualifier is gone, or the genus
    /// or species is another. A name that was sure before is never identified.
    /// </summary>
    public static bool WasIdentified(
        (string? Genus, string? Species, NameQualifier? Qualifier) before,
        (string? Genus, string? Species, NameQualifier? Qualifier) after) =>
        before.Qualifier is not null
        && (after.Qualifier is null || Genus(before.Genus) != Genus(after.Genus) || Species(before.Species) != Species(after.Species));

    /// <summary>The locality or collector code, trimmed. Null when nothing was typed.</summary>
    public static string? Note(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    /// <summary>
    /// The species to look things up by: none for sp. and a lost name, since a stored species
    /// isn't shown for those either.
    /// </summary>
    public static string? SpeciesToLookUp(string? species, NameQualifier? qualifier) =>
        qualifier is NameQualifier.Sp or NameQualifier.Unknown ? null : species;

    /// <summary>The errors in the name and how sure it is, shared by plants, propagations and wishes.</summary>
    public static IEnumerable<string> QualifierErrors(string? genus, string? species, NameQualifier? qualifier)
    {
        if (qualifier is NameQualifier.Cf or NameQualifier.Aff && string.IsNullOrWhiteSpace(species))
            yield return "cf. and aff. go with a species name.";
        if (qualifier == NameQualifier.Sp && string.IsNullOrWhiteSpace(genus))
            yield return "sp. goes with a genus.";
    }

    /// <summary>"cf.", "aff.", "sp." or "name lost". Null when the name is sure.</summary>
    public static string? QualifierText(NameQualifier? qualifier) => qualifier switch
    {
        NameQualifier.Cf => "cf.",
        NameQualifier.Aff => "aff.",
        NameQualifier.Sp => "sp.",
        NameQualifier.Unknown => "name lost",
        _ => null
    };

    /// <summary>Reads "cf", "aff.", "sp", "unknown", "name lost" and so on. Null for anything else.</summary>
    public static NameQualifier? ParseQualifier(string? text) => text?.Trim().TrimEnd('.').Trim().ToLowerInvariant() switch
    {
        "cf" => NameQualifier.Cf,
        "aff" => NameQualifier.Aff,
        "sp" => NameQualifier.Sp,
        "unknown" or "name lost" or "lost" or "?" => NameQualifier.Unknown,
        _ => null
    };

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
