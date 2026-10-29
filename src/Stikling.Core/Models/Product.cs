using System.Globalization;
using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

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
/// A nutrient a product can say it contains. Stored as text, so old data doesn't depend on the order, but
/// the order here is the order they are shown in after N-P-K.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Nutrient>))]
public enum Nutrient
{
    Nitrogen,
    Phosphorus,
    Potassium,
    Calcium,
    Magnesium,
    Sulphur,
    Silicon,
    Iron,
    Manganese,
    Zinc,
    Boron,
    Copper,
    Molybdenum
}

/// <summary>How much of one nutrient a product says it has, in percent, as printed on the label.</summary>
public sealed class NutrientAmount
{
    public Nutrient Nutrient { get; set; }

    public decimal Percent { get; set; }
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

    private List<NutrientAmount> nutrients = [];

    /// <summary>
    /// What the label says the product contains, in percent. The numbers are copied as printed:
    /// labels give phosphorus and potassium as oxides (P2O5 and K2O), and the app doesn't convert.
    /// Old data and a null in JSON both read as an empty list.
    /// </summary>
    public List<NutrientAmount> Nutrients
    {
        get => nutrients;
        set => nutrients = value ?? [];
    }

    private static readonly Nutrient[] Npk = [Nutrient.Nitrogen, Nutrient.Phosphorus, Nutrient.Potassium];

    /// <summary>"3-1-2" when the product has nitrogen, phosphorus or potassium (a missing one is 0), otherwise null.</summary>
    [JsonIgnore]
    public string? NpkText => Nutrients.Any(n => Npk.Contains(n.Nutrient))
        ? string.Join("-", Npk
            .Select(nutrient => Doses.Number(Nutrients.FirstOrDefault(n => n.Nutrient == nutrient)?.Percent ?? 0)))
        : null;

    /// <summary>"NPK 3-1-2 · Ca 2 · Mg 1", or null when the product lists no nutrients.</summary>
    [JsonIgnore]
    public string? NutrientText
    {
        get
        {
            var parts = new List<string>();
            if (NpkText is { } npk)
                parts.Add($"NPK {npk}");
            parts.AddRange(Nutrients
                .Where(n => !Npk.Contains(n.Nutrient))
                .OrderBy(n => n.Nutrient)
                .Select(n => $"{NutrientInfo.Symbol(n.Nutrient)} {Doses.Number(n.Percent)}"));
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }

    /// <summary>"2 ml/L" or "5 ml per 4 L", or null when there's no default dose.</summary>
    [JsonIgnore]
    public string? DoseText => DefaultDose is { } dose ? Doses.Text(dose, Unit, Per, PerUnit) : null;

    public Product Copy()
    {
        var copy = (Product)MemberwiseClone();
        copy.nutrients = [.. Nutrients.Select(n => new NutrientAmount { Nutrient = n.Nutrient, Percent = n.Percent })];
        return copy;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add(Strings.Supplies.ProductNeedsName);

        if (DefaultDose < 0)
            errors.Add(Strings.Supplies.DoseNegative);

        if (Per <= 0)
            errors.Add(Doses.NoWater);

        if (Nutrients.Any(n => n.Percent is < 0 or > 100))
            errors.Add(Strings.Supplies.NutrientRange);

        if (Nutrients.GroupBy(n => n.Nutrient).Any(g => g.Count() > 1))
            errors.Add(Strings.Supplies.NutrientOnce);

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

public static class NutrientInfo
{
    /// <summary>"N", "Ca", "Si": the chemical symbols, as a label prints them.</summary>
    public static string Symbol(Nutrient nutrient) => nutrient switch
    {
        Nutrient.Nitrogen => "N",
        Nutrient.Phosphorus => "P",
        Nutrient.Potassium => "K",
        Nutrient.Calcium => "Ca",
        Nutrient.Magnesium => "Mg",
        Nutrient.Sulphur => "S",
        Nutrient.Silicon => "Si",
        Nutrient.Iron => "Fe",
        Nutrient.Manganese => "Mn",
        Nutrient.Zinc => "Zn",
        Nutrient.Boron => "B",
        Nutrient.Copper => "Cu",
        _ => "Mo"
    };
}

public static class Doses
{
    public static string NoWater => Strings.Supplies.NoWater;

    /// <summary>"ml", "g", "drops". Symbols rather than prose, so they live here and not with the labels.</summary>
    public static string Symbol(DoseUnit unit) => unit switch
    {
        DoseUnit.Grams => "g",
        DoseUnit.Drops => Strings.Supplies.DropsSymbol,
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
            : string.Format(CultureInfo.CurrentCulture, Strings.Supplies.DoseRate, Measured(amount, unit), Number(per), Symbol(perUnit));

    /// <summary>"4 ml", "1.5 g", "3 drops": an amount to measure out.</summary>
    public static string Measured(decimal amount, DoseUnit unit) => unit switch
    {
        DoseUnit.Grams => $"{Number(amount)} g",
        DoseUnit.Drops => string.Format(CultureInfo.CurrentCulture, amount == 1 ? Strings.Supplies.DoseDrops_One : Strings.Supplies.DoseDrops_Other, Number(amount)),
        _ => $"{Number(amount)} ml"
    };

    /// <summary>"2 L", "0.75 L", or "500 ml" under a litre, for an amount of water.</summary>
    public static string Water(decimal litres) =>
        litres < 1 ? $"{Number(litres * 1000)} ml" : $"{Number(litres)} L";

    public static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
