namespace Stikling.Core.Identification;

/// <summary>How well a suggestion matches, in the words the sheet uses.</summary>
public enum IdentificationMatch
{
    Good,
    Possible,
    LongShot,
}

public static class IdentificationRules
{
    public const double GoodScore = 0.5;
    public const double PossibleScore = 0.15;

    /// <summary>How many suggestions are shown.</summary>
    public const int MaxSuggestions = 5;

    public static IdentificationMatch Match(double score) =>
        score >= GoodScore ? IdentificationMatch.Good
        : score >= PossibleScore ? IdentificationMatch.Possible
        : IdentificationMatch.LongShot;

    /// <summary>
    /// The cultivar after a suggestion is picked. A cultivar belongs to a species, so it is kept when
    /// the genus and species stay the same and cleared when they change.
    /// </summary>
    public static string? CultivarAfterPick(string? genus, string? species, string? cultivar, string newGenus, string newSpecies) =>
        Same(genus, newGenus) && Same(species, newSpecies) ? cultivar : null;

    private static bool Same(string? a, string b) =>
        string.Equals(a?.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
