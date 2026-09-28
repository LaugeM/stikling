namespace Stikling.Core.Models;

/// <summary>
/// A treatment you make up yourself, saved under a name: "alcohol spray", "neem drench". The
/// ingredients are typed rather than picked from the products, since alcohol and dish soap
/// don't belong next to the fertilisers. Amounts are optional, and how to use it is free text.
///
/// A treatment logged with a recipe copies it, so editing the recipe later doesn't change what
/// was logged before.
/// </summary>
public sealed class TreatmentRecipe : Entity
{
    public string? Name { get; set; }

    /// <summary>What goes in, in the order it goes in. Whatever isn't listed is water.</summary>
    public List<RecipeIngredient> Ingredients { get; set; } = [];

    /// <summary>How to use it, e.g. "Both sides of the leaves, then the top of the soil".</summary>
    public string? Method { get; set; }

    /// <summary>A copy with its own ingredient list, so editing a draft can't change the original.</summary>
    public TreatmentRecipe Copy()
    {
        var copy = (TreatmentRecipe)MemberwiseClone();
        copy.Ingredients = [.. Ingredients.Select(i => i.Copy())];
        return copy;
    }

    public void MoveUp(int index)
    {
        if (index > 0 && index < Ingredients.Count)
            (Ingredients[index - 1], Ingredients[index]) = (Ingredients[index], Ingredients[index - 1]);
    }

    public void MoveDown(int index)
    {
        if (index >= 0 && index < Ingredients.Count - 1)
            (Ingredients[index], Ingredients[index + 1]) = (Ingredients[index + 1], Ingredients[index]);
    }

    /// <summary>The ingredients as a treatment keeps them, each a copy.</summary>
    public List<RecipeIngredient> CopyIngredients() => [.. Ingredients.Select(i => i.Copy())];

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the recipe a name.");

        errors.AddRange(RecipeIngredient.Validate(Ingredients));

        return errors;
    }
}

/// <summary>
/// One thing in a recipe. The amount is for that much of the finished mix, as it was typed:
/// 250 ml per 1 L of spray, or 2 drops per 500 ml.
/// </summary>
public sealed class RecipeIngredient
{
    public string Name { get; set; } = "";

    /// <summary>Optional, since "a drop of dish soap" doesn't always get measured.</summary>
    public decimal? Amount { get; set; }

    public DoseUnit Unit { get; set; } = DoseUnit.Millilitres;

    /// <summary>How much of the finished mix <see cref="Amount"/> is for.</summary>
    public decimal Per { get; set; } = 1;

    public WaterUnit PerUnit { get; set; } = WaterUnit.Litres;

    public RecipeIngredient Copy() => (RecipeIngredient)MemberwiseClone();

    /// <summary>
    /// How much to measure out for that much of the mix, or null when there's no amount.
    /// 250 ml per 1 L comes to 125 ml for a 500 ml bottle.
    /// </summary>
    public decimal? For(decimal litres) =>
        Doses.ToLitres(Per, PerUnit) is > 0 and var per ? Amount * litres / per : null;

    /// <summary>"Isopropyl alcohol, 250 ml/L", or just the name when no amount was given.</summary>
    public override string ToString() =>
        Amount is { } amount ? $"{Name.Trim()}, {Doses.Text(amount, Unit, Per, PerUnit)}" : Name.Trim();

    /// <summary>The same rules for a recipe's ingredients and the copy a treatment keeps.</summary>
    public static IEnumerable<string> Validate(IReadOnlyList<RecipeIngredient> ingredients)
    {
        if (ingredients.Any(i => string.IsNullOrWhiteSpace(i.Name)))
            yield return "Every ingredient needs a name.";

        if (ingredients.Any(i => i.Amount < 0))
            yield return "An amount can't be less than 0.";

        if (ingredients.Any(i => i.Per <= 0))
            yield return "What an amount is for has to be more than 0.";
    }
}
