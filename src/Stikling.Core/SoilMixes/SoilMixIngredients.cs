using Stikling.Core.Models;

namespace Stikling.Core.SoilMixes;

/// <summary>
/// What a mix is usually made of. Only suggestions: anything can be typed, the same way a genus
/// can be typed that isn't in the common list.
/// </summary>
public static class SoilMixIngredients
{
    public static readonly IReadOnlyList<string> Common =
    [
        "Potting soil",
        "Perlite",
        "LECA",
        "Bark",
        "Pumice",
        "Coco coir",
        "Sphagnum",
        "Worm castings",
        "Sand",
        "Charcoal"
    ];

    /// <summary>The common ones plus anything already used in a mix, without repeats.</summary>
    public static IReadOnlyList<string> Suggestions(IEnumerable<SoilMix> mixes)
    {
        var used = mixes
            .Where(mix => !mix.IsDeleted)
            .SelectMany(mix => mix.Ingredients)
            .Select(i => i.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!);

        return [.. Common.Concat(used).Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
