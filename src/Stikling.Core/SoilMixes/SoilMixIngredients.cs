using Stikling.Core.Models;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.SoilMixes;

/// <summary>
/// What a mix is usually made of. Only suggestions: anything can be typed, the same way a genus
/// can be typed that isn't in the common list.
/// </summary>
public static class SoilMixIngredients
{
    public static IReadOnlyList<string> Common =>
    [
        Strings.Supplies.IngredientPottingSoil,
        Strings.Supplies.IngredientPerlite,
        Strings.Supplies.IngredientLeca,
        Strings.Supplies.IngredientBark,
        Strings.Supplies.IngredientPumice,
        Strings.Supplies.IngredientCocoCoir,
        Strings.Supplies.IngredientSphagnum,
        Strings.Supplies.IngredientWormCastings,
        Strings.Supplies.IngredientSand,
        Strings.Supplies.IngredientCharcoal
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
