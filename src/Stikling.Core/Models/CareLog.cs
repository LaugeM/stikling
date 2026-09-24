using System.Text.Json.Serialization;

namespace Stikling.Core.Models;

/// <summary>Something done to a plant. Stored as text like the other enums.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CareKind>))]
public enum CareKind
{
    Watered,
    Fertilised,

    /// <summary>Semi-hydro: the reservoir was filled back up.</summary>
    ToppedUp,

    /// <summary>Semi-hydro: rinsed through to wash out built-up salts.</summary>
    Flushed,

    Repotted,
    Pruned,
    Rotated,
    LeavesCleaned,
    Harvested,

    /// <summary>A reading off the moisture meter, 1 to 10.</summary>
    MoistureReading
}

/// <summary>
/// One entry in a plant's care log: watered, fertilised, repotted and so on. Kept separate
/// from the timeline so "last watered" is a lookup rather than a search through history text.
/// </summary>
public sealed class CareLog : Entity
{
    public Guid PlantId { get; set; }

    /// <summary>The day it was done, which can be earlier than when it was written down.</summary>
    public DateOnly OccurredOn { get; set; }

    public CareKind Kind { get; set; }

    /// <summary>The moisture meter reading, 1 to 10. Only on <see cref="CareKind.MoistureReading"/>.</summary>
    public int? Moisture { get; set; }

    /// <summary>
    /// What went in the water, in the order it went in. Only on the kinds that take a product,
    /// and each one is a copy of the product as it was that day.
    /// </summary>
    public List<ProductDose> Products { get; set; } = [];

    public string? Notes { get; set; }

    public CareLog Copy()
    {
        var copy = (CareLog)MemberwiseClone();
        copy.Products = [.. Products.Select(p => p.Copy())];
        return copy;
    }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();

        if (PlantId == Guid.Empty)
            errors.Add("A care entry has to belong to a plant.");

        if (OccurredOn > today)
            errors.Add("That date is in the future.");

        if (Kind == CareKind.MoistureReading)
        {
            if (Moisture is null)
                errors.Add("Give the moisture reading a number from 1 to 10.");
            else if (Moisture is < 1 or > 10)
                errors.Add("The moisture reading goes from 1 to 10.");
        }
        else if (Moisture is not null)
        {
            errors.Add("Only a moisture reading has a number.");
        }

        if (Products.Count > 0 && !CareKinds.TakesProducts(Kind))
            errors.Add("Only fertilising and topping up can have a product.");

        if (Products.Any(p => string.IsNullOrWhiteSpace(p.Name)))
            errors.Add("A product on the entry needs a name.");

        if (Products.Any(p => p.Amount < 0))
            errors.Add("A dose can't be less than 0.");

        return errors;
    }
}

/// <summary>Which kinds of care are worth a line in the plant's history.</summary>
public static class CareKinds
{
    /// <summary>
    /// Repotting, flushing and pruning change the plant enough to belong in its story. Watering
    /// and the rest would bury the photos and notes within a month, so they stay in the care log.
    /// </summary>
    public static bool IsNotable(CareKind kind) =>
        kind is CareKind.Repotted or CareKind.Flushed or CareKind.Pruned;

    /// <summary>
    /// Fertilising, and topping up a semi-hydro reservoir, which is usually done with the
    /// fertilised water rather than plain.
    /// </summary>
    public static bool TakesProducts(CareKind kind) =>
        kind is CareKind.Fertilised or CareKind.ToppedUp;
}
