using System.Text.RegularExpressions;

namespace Stikling.Core.PlantFacts;

/// <summary>Checks and tidies the names the plant facts endpoint is asked about.</summary>
public static partial class PlantFactNames
{
    public const int MaxLength = 60;

    [GeneratedRegex(@"^[\p{L}\- ×]+$")]
    private static partial Regex Allowed();

    /// <summary>Letters, hyphens, × and spaces, at most 60 characters.</summary>
    public static bool IsValid(string? name) =>
        name is { Length: > 0 and <= MaxLength } && name.Trim().Length > 0 && Allowed().IsMatch(name);

    /// <summary>The genus with a capital first letter and the rest in lower case, as botanical names are written.</summary>
    public static string Genus(string genus)
    {
        var g = Tidy(genus);
        return g.Length == 0 ? g : char.ToUpperInvariant(g[0]) + g[1..].ToLowerInvariant();
    }

    public static string Species(string species) => Tidy(species).ToLowerInvariant();

    private static string Tidy(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The Wikipedia article title: Genus_species, or just Genus.</summary>
    public static string WikipediaTitle(string genus, string? species) =>
        (species is { Length: > 0 } ? $"{genus} {species}" : genus).Replace(' ', '_');

    /// <summary>Trefle's slug for a species: lower case with hyphens, like monstera-deliciosa.</summary>
    public static string TrefleSlug(string genus, string species) =>
        string.Join('-', $"{genus} {species}".ToLowerInvariant().Replace("×", " ")
            .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));
}
