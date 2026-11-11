using Stikling.Core.Models;
using Stikling.Core.SoilMixes;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Stock;

/// <summary>The names offered under the name of a new supply. Only suggestions: anything can be typed.</summary>
public static class SupplySuggestions
{
    /// <summary>
    /// For soil mixes, what mixes are made of. For pests, the usual ones plus the ingredients of the
    /// treatment recipes. Nothing for the water, where the person's products are offered, or for the rest.
    /// Names already in the stock are left out, and there are no repeats.
    /// </summary>
    public static IReadOnlyList<string> For(
        SupplyKind kind, IEnumerable<SoilMix> mixes, IEnumerable<TreatmentRecipe> recipes, IEnumerable<string> stocked)
    {
        IEnumerable<string> names = kind switch
        {
            SupplyKind.Mixing => SoilMixIngredients.Suggestions(mixes),
            SupplyKind.Pests => Pests(recipes),
            _ => []
        };

        var have = stocked.Select(n => n.Trim()).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        return [.. names.Where(n => !have.Contains(n.Trim()))];
    }

    public static IReadOnlyList<string> Pests(IEnumerable<TreatmentRecipe> recipes)
    {
        string[] common =
        [
            Strings.Stock.SuggestStickyTraps,
            Strings.Stock.SuggestAlcohol,
            Strings.Stock.SuggestNeem,
            Strings.Stock.SuggestSoap,
            Strings.Stock.SuggestMites
        ];

        var fromRecipes = recipes
            .Where(r => !r.IsDeleted)
            .SelectMany(r => r.Ingredients)
            .Select(i => i.Name?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!);

        return [.. common.Concat(fromRecipes).Distinct(StringComparer.CurrentCultureIgnoreCase)];
    }
}
