using System.Globalization;
using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>What a dose is measured in. Always per litre of water, so doses can be compared and scaled.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DoseUnit>))]
public enum DoseUnit
{
    MlPerLitre,
    GramsPerLitre,
    DropsPerLitre
}

/// <summary>
/// Something that goes in the water: a fertiliser, and later the stimulants and silica that
/// make up a feed. Saved once with the dose you usually use, so logging a feed is a tap.
/// </summary>
public sealed class Product : Entity
{
    public string? Name { get; set; }

    /// <summary>The dose you usually use. Optional, because the bottle isn't always to hand.</summary>
    public decimal? DefaultDose { get; set; }

    public DoseUnit Unit { get; set; } = DoseUnit.MlPerLitre;

    public string? Notes { get; set; }

    /// <summary>"2 ml/L", or null when there's no default dose.</summary>
    [JsonIgnore]
    public string? DoseText => DefaultDose is { } dose ? Doses.Text(dose, Unit) : null;

    public Product Copy() => (Product)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the product a name.");

        if (DefaultDose < 0)
            errors.Add("A dose can't be less than 0.");

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

    public DoseUnit Unit { get; set; } = DoseUnit.MlPerLitre;

    /// <summary>A product at its default dose, ready to change for this one entry.</summary>
    public static ProductDose From(Product product) => new()
    {
        ProductId = product.Id,
        Name = product.Name?.Trim() ?? "",
        Amount = product.DefaultDose,
        Unit = product.Unit
    };

    public ProductDose Copy() => (ProductDose)MemberwiseClone();

    /// <summary>"Hydro fertiliser, 2 ml/L", or just the name when no dose was given.</summary>
    public override string ToString() =>
        Amount is { } amount ? $"{Name.Trim()}, {Doses.Text(amount, Unit)}" : Name.Trim();
}

public static class Doses
{
    /// <summary>"ml/L". A symbol rather than prose, so it lives here and not with the labels.</summary>
    public static string Symbol(DoseUnit unit) => unit switch
    {
        DoseUnit.GramsPerLitre => "g/L",
        DoseUnit.DropsPerLitre => "drops/L",
        _ => "ml/L"
    };

    /// <summary>"2 ml/L", "0.5 g/L".</summary>
    public static string Text(decimal amount, DoseUnit unit) =>
        $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} {Symbol(unit)}";
}
