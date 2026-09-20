using System.Globalization;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>
/// How the amounts in a mix are counted, if they're counted at all. A mix you mix by eye is
/// still a mix, so None is the default and a valid state, not a missing value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MixUnit>))]
public enum MixUnit
{
    None,
    Parts,
    Percent
}

/// <summary>One thing in a mix. The amount is optional, because it often isn't known.</summary>
public sealed class MixIngredient
{
    public string Name { get; set; } = "";

    public decimal? Amount { get; set; }
}

/// <summary>
/// A soil mix you make yourself, saved under a name.
///
/// The order of the ingredients is the content: the first one is what there's most of. That's
/// how a mix gets described when the exact amounts were never measured, which is the usual case
/// when a batch gets topped up over time. Nothing is ever sorted automatically.
/// </summary>
public sealed class SoilMix : Entity
{
    public string? Name { get; set; }

    public string? Notes { get; set; }

    public MixUnit Unit { get; set; } = MixUnit.None;

    /// <summary>What's in it, most of first.</summary>
    public List<MixIngredient> Ingredients { get; set; } = [];

    /// <summary>
    /// When it stopped being mixed. A retired mix is off the picker, so old versions don't pile
    /// up in front of you, but the plants already in it still say so.
    /// </summary>
    public DateOnly? RetiredOn { get; set; }

    [JsonIgnore]
    public bool IsRetired => RetiredOn is not null;

    [JsonIgnore]
    public decimal Total => Ingredients.Sum(i => i.Amount ?? 0);

    /// <summary>True unless the mix is in percent and they don't reach 100.</summary>
    [JsonIgnore]
    public bool PercentAddsUp => Unit != MixUnit.Percent || Total == 100;

    /// <summary>The ingredients in order, for the line under a mix: "potting soil, bark, LECA".</summary>
    [JsonIgnore]
    public string Recipe => string.Join(", ", Named().Select(Describe));

    private string Describe(MixIngredient ingredient) => (Unit, ingredient.Amount) switch
    {
        (MixUnit.Parts, { } amount) => $"{Number(amount)} × {ingredient.Name.Trim()}",
        (MixUnit.Percent, { } amount) => $"{ingredient.Name.Trim()} {Number(amount)}%",
        _ => ingredient.Name.Trim()
    };

    public static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private IEnumerable<MixIngredient> Named() =>
        Ingredients.Where(i => !string.IsNullOrWhiteSpace(i.Name));

    /// <summary>A copy with its own ingredient list, so editing a draft can't change the original.</summary>
    public SoilMix Copy()
    {
        var copy = (SoilMix)MemberwiseClone();
        copy.Ingredients = [.. Ingredients.Select(i => new MixIngredient { Name = i.Name, Amount = i.Amount })];
        return copy;
    }

    /// <summary>The same mix under a new id, for saving an edit without changing what's in use.</summary>
    public SoilMix CopyAsNew(string name)
    {
        var copy = Copy();
        copy.Id = Guid.NewGuid();
        copy.Name = name;
        copy.CreatedAt = copy.UpdatedAt = default;
        copy.DeletedAt = null;
        copy.RetiredOn = null;
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

    /// <summary>
    /// Drops the empty rows, and the amounts too when the mix doesn't count them, so what's
    /// stored is what was on screen.
    /// </summary>
    public void Tidy()
    {
        Ingredients = [.. Named().Select(i => new MixIngredient
        {
            Name = i.Name.Trim(),
            Amount = Unit == MixUnit.None ? null : i.Amount
        })];
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the mix a name.");

        if (!Named().Any())
            errors.Add("Add at least one ingredient.");

        if (Ingredients.Any(i => i.Amount < 0))
            errors.Add("An amount can't be less than 0.");

        // Percentages that don't reach 100 are said out loud, not refused: a mix you only half
        // remember is still worth writing down.
        return errors;
    }
}
