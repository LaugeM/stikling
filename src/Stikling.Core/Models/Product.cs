using System.Globalization;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>What the product itself is measured in. The water it goes into is <see cref="WaterUnit"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DoseUnit>))]
public enum DoseUnit
{
    Millilitres,
    Grams,
    Drops
}

/// <summary>What the water in a dose or a batch is measured in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WaterUnit>))]
public enum WaterUnit
{
    Litres,
    Millilitres
}

/// <summary>
/// What sort of thing a product is. Mostly for reading a feed back, and for knowing that silica
/// goes in first and pH last.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProductKind>))]
public enum ProductKind
{
    Fertiliser,

    /// <summary>A rooting or growth stimulant.</summary>
    Stimulant,

    Silica,
    CalMag,

    /// <summary>pH up or pH down.</summary>
    Ph,

    Other
}

/// <summary>
/// Something that goes in the water: a fertiliser, a stimulant, silica and the rest of what
/// makes up a feed. Saved once with the dose you usually use, so logging a feed is a tap.
/// </summary>
public sealed class Product : Entity
{
    public string? Name { get; set; }

    public ProductKind Kind { get; set; } = ProductKind.Fertiliser;

    /// <summary>The dose you usually use. Optional, because the bottle isn't always to hand.</summary>
    public decimal? DefaultDose { get; set; }

    public DoseUnit Unit { get; set; } = DoseUnit.Millilitres;

    /// <summary>
    /// How much water the dose is for, as the bottle puts it: 5 ml per 4 L is saved as 5, 4 and
    /// litres rather than worked out to 1.25 ml/L.
    /// </summary>
    public decimal Per { get; set; } = 1;

    public WaterUnit PerUnit { get; set; } = WaterUnit.Litres;

    public string? Notes { get; set; }

    /// <summary>"2 ml/L" or "5 ml per 4 L", or null when there's no default dose.</summary>
    [JsonIgnore]
    public string? DoseText => DefaultDose is { } dose ? Doses.Text(dose, Unit, Per, PerUnit) : null;

    public Product Copy() => (Product)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the product a name.");

        if (DefaultDose < 0)
            errors.Add("A dose can't be less than 0.");

        if (Per <= 0)
            errors.Add(Doses.NoWater);

        return errors;
    }
}

/// <summary>
/// A product as it was used on one care entry. The name and dose are copied rather than looked
/// up, so editing or deleting the product later doesn't change what an old entry says.
/// </summary>
public sealed class ProductDose
{
    /// <summary>The product it came from, for finding the entries that used it.</summary>
    public Guid? ProductId { get; set; }

    public string Name { get; set; } = "";

    public decimal? Amount { get; set; }

    public DoseUnit Unit { get; set; } = DoseUnit.Millilitres;

    /// <summary>How much water <see cref="Amount"/> is for, as it was typed.</summary>
    public decimal Per { get; set; } = 1;

    public WaterUnit PerUnit { get; set; } = WaterUnit.Litres;

    /// <summary>A product at its default dose, ready to change for this one entry.</summary>
    public static ProductDose From(Product product) => new()
    {
        ProductId = product.Id,
        Name = product.Name?.Trim() ?? "",
        Amount = product.DefaultDose,
        Unit = product.Unit,
        Per = product.Per,
        PerUnit = product.PerUnit
    };

    public ProductDose Copy() => (ProductDose)MemberwiseClone();

    /// <summary>
    /// How much to measure out for that much water, or null when there's no dose. 5 ml per 4 L
    /// comes to 2.5 ml for 2 L.
    /// </summary>
    public decimal? For(decimal litres) =>
        Doses.ToLitres(Per, PerUnit) is > 0 and var per ? Amount * litres / per : null;

    /// <summary>"Hydro fertiliser, 2 ml/L", or just the name when no dose was given.</summary>
    public override string ToString() =>
        Amount is { } amount ? $"{Name.Trim()}, {Doses.Text(amount, Unit, Per, PerUnit)}" : Name.Trim();
}

public static class Doses
{
    public const string NoWater = "The water a dose is for has to be more than 0.";

    /// <summary>"ml", "g", "drops". Symbols rather than prose, so they live here and not with the labels.</summary>
    public static string Symbol(DoseUnit unit) => unit switch
    {
        DoseUnit.Grams => "g",
        DoseUnit.Drops => "drops",
        _ => "ml"
    };

    public static string Symbol(WaterUnit unit) => unit == WaterUnit.Millilitres ? "ml" : "L";

    public static decimal ToLitres(decimal amount, WaterUnit unit) =>
        unit == WaterUnit.Millilitres ? amount / 1000 : amount;

    /// <summary>
    /// "2 ml/L" for a dose per litre, the way most bottles put it, and "5 ml per 4 L" or
    /// "1 drop per 500 ml" for anything else, so it reads the way it was typed.
    /// </summary>
    public static string Text(decimal amount, DoseUnit unit, decimal per = 1, WaterUnit perUnit = WaterUnit.Litres) =>
        per == 1 && perUnit == WaterUnit.Litres
            ? $"{Number(amount)} {Symbol(unit)}/L"
            : $"{Measured(amount, unit)} per {Number(per)} {Symbol(perUnit)}";

    /// <summary>"4 ml", "1.5 g", "3 drops": an amount to measure out.</summary>
    public static string Measured(decimal amount, DoseUnit unit) => unit switch
    {
        DoseUnit.Grams => $"{Number(amount)} g",
        DoseUnit.Drops => amount == 1 ? "1 drop" : $"{Number(amount)} drops",
        _ => $"{Number(amount)} ml"
    };

    /// <summary>"2 L", "0.75 L", or "500 ml" under a litre, for an amount of water.</summary>
    public static string Water(decimal litres) =>
        litres < 1 ? $"{Number(litres * 1000)} ml" : $"{Number(litres)} L";

    public static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
