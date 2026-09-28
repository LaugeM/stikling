using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>
/// Whether something was used on the plants, they were only looked over, or a sticky trap was
/// counted.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PestTreatmentKind>))]
public enum PestTreatmentKind
{
    Treated,

    /// <summary>Looked the plants over while the case is being watched.</summary>
    Checked,

    /// <summary>Counted what's on a sticky trap, put up a new one, or both.</summary>
    TrapCount
}

/// <summary>
/// One treatment on a case: a spray, a wipe-down, a soil drench. Or, once the case is only being
/// watched, a check that the pests haven't come back, or a count of a sticky trap. It belongs to
/// the case rather than to a plant, because a case is treated as a whole.
/// </summary>
public sealed class PestTreatment : Entity
{
    public Guid CaseId { get; set; }

    public PestTreatmentKind Kind { get; set; } = PestTreatmentKind.Treated;

    /// <summary>The day it was done, which can be earlier than when it was written down.</summary>
    public DateOnly OccurredOn { get; set; }

    /// <summary>
    /// What was used, e.g. "alcohol spray, both sides of the leaves". With a recipe it's the
    /// recipe's name as it was that day.
    /// </summary>
    public string? What { get; set; }

    /// <summary>The recipe it was made from, for counting how often each one is used.</summary>
    public Guid? RecipeId { get; set; }

    /// <summary>
    /// What went in, copied from the recipe on the day, so editing or deleting the recipe
    /// leaves this entry alone. Empty when no recipe was used.
    /// </summary>
    public List<RecipeIngredient> Ingredients { get; set; } = [];

    /// <summary>How the recipe said to use it, copied the same way.</summary>
    public string? Method { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// When the next treatment is due, when it shouldn't simply be the case's interval. Left
    /// empty the interval decides, so this is only filled in when you want a different gap.
    /// </summary>
    public DateOnly? NextDueOn { get; set; }

    /// <summary>
    /// A trap count: how many are on the trap in all, not how many are new. The app works out
    /// what was caught since last time. Null when a new trap was only put up.
    /// </summary>
    public int? OnTrap { get; set; }

    /// <summary>
    /// A trap count: a fresh trap went up that day, after counting the old one if there was a
    /// count, so the next count starts from nothing.
    /// </summary>
    public bool NewTrap { get; set; }

    public PestTreatment Copy()
    {
        var copy = (PestTreatment)MemberwiseClone();
        copy.Ingredients = [.. Ingredients.Select(i => i.Copy())];
        return copy;
    }

    /// <summary>Fills in the recipe as it is today, replacing whatever was typed.</summary>
    public void Use(TreatmentRecipe recipe)
    {
        RecipeId = recipe.Id;
        What = recipe.Name?.Trim();
        Ingredients = recipe.CopyIngredients();
        Method = string.IsNullOrWhiteSpace(recipe.Method) ? null : recipe.Method.Trim();
    }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();

        if (CaseId == Guid.Empty)
            errors.Add("A treatment has to belong to a case.");

        if (OccurredOn > today)
            errors.Add("That date is in the future.");

        if (NextDueOn is { } next && next < OccurredOn)
            errors.Add("The next treatment can't be due before this one happened.");

        var treated = What is not null || NextDueOn is not null || RecipeId is not null || Ingredients.Count > 0 || Method is not null;
        var trapped = OnTrap is not null || NewTrap;

        if (Kind == PestTreatmentKind.Checked && (treated || trapped))
            errors.Add("A check only has a date and notes.");

        if (Kind == PestTreatmentKind.Treated && trapped)
            errors.Add("A treatment doesn't have a trap count.");

        if (Kind == PestTreatmentKind.TrapCount)
        {
            if (treated)
                errors.Add("A trap count only has the count, a new trap, a date and notes.");
            else if (OnTrap is null && !NewTrap)
                errors.Add("Fill in how many are on the trap, or that a new one went up.");

            if (OnTrap < 0)
                errors.Add("The count can't be below zero.");
        }

        errors.AddRange(RecipeIngredient.Validate(Ingredients));

        return errors;
    }
}
