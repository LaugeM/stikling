using Stikling.Core.Models;

namespace Stikling.Core.Pests;

/// <summary>A recipe, how many times it has been used, and when it was last used.</summary>
public sealed record RecipeUse(TreatmentRecipe Recipe, int Times, DateOnly? LastUsed)
{
    /// <summary>Every recipe with its use counted from the treatments, sorted by name.</summary>
    public static IReadOnlyList<RecipeUse> List(IEnumerable<TreatmentRecipe> recipes, IEnumerable<PestTreatment> treatments)
    {
        var used = treatments
            .Where(t => !t.IsDeleted && t.RecipeId is not null)
            .ToLookup(t => t.RecipeId!.Value);

        return recipes
            .Where(r => !r.IsDeleted)
            .Select(recipe =>
            {
                var times = used[recipe.Id].ToList();
                return new RecipeUse(
                    recipe,
                    times.Count,
                    times.Count == 0 ? null : times.Max(t => t.OccurredOn));
            })
            .OrderBy(use => use.Recipe.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// What a treatment recipe is usually made of. Only suggestions: anything can be typed, the same
/// way a soil mix can have an ingredient that isn't on its list.
/// </summary>
public static class RecipeIngredients
{
    public static readonly IReadOnlyList<string> Common =
    [
        "Isopropyl alcohol",
        "Dish soap",
        "Neem oil",
        "Insecticidal soap",
        "Hydrogen peroxide"
    ];

    /// <summary>The common ones plus anything already used in a recipe, without repeats.</summary>
    public static IReadOnlyList<string> Suggestions(IEnumerable<TreatmentRecipe> recipes)
    {
        var used = recipes
            .Where(recipe => !recipe.IsDeleted)
            .SelectMany(recipe => recipe.Ingredients)
            .Select(i => i.Name.Trim())
            .Where(name => name.Length > 0);

        return [.. Common.Concat(used).Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
