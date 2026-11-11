using Stikling.Core.Models;

namespace Stikling.Core.Names;

/// <summary>
/// The key two names are compared by: genus, species and cultivar without case or quotes, with an old
/// genus or species followed to the one it goes by now. Shared by the wishlist and the list comparison.
/// </summary>
internal static class PlantNameKey
{
    public static (string Genus, string Species, string Cultivar) Of(string? genus, string? species, string? cultivar, PlantDictionary? dictionary)
    {
        var (g, s) = dictionary?.Current(genus, species) ?? (genus, species);
        return (Clean(g) ?? "", Clean(s) ?? "", Clean(PlantNames.Cultivar(cultivar)) ?? "");
    }

    /// <summary>Trimmed and lower case, or null when nothing is left.</summary>
    public static string? Clean(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToLowerInvariant();
}
