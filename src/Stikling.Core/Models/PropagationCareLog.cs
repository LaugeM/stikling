using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// One entry in a propagation's care log: watered, changed the water or a moisture reading.
/// Kept apart from <see cref="CareLog"/>, which belongs to plants. When the propagation is
/// potted up, its last watering is copied to the new plants as a plant care entry.
/// </summary>
public sealed class PropagationCareLog : Entity
{
    public Guid PropagationId { get; set; }

    /// <summary>The day it was done, which can be earlier than when it was written down.</summary>
    public DateOnly OccurredOn { get; set; }

    public PropagationCareKind Kind { get; set; }

    /// <summary>The moisture meter reading, 0 to 10. Only on <see cref="PropagationCareKind.MoistureReading"/>.</summary>
    public int? Moisture { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// What went in the water, in the order it went in. Only on a watering or a water change, and each
    /// one is a copy of the product as it was that day. Mirrors <see cref="CareLog.Products"/>.
    /// </summary>
    public List<ProductDose> Products { get; set; } = [];

    /// <summary>The saved feed the products came from.</summary>
    public Guid? FeedId { get; set; }

    /// <summary>The feed's name as it was that day, so renaming or deleting it leaves the entry alone.</summary>
    public string? FeedName { get; set; }

    /// <summary>How much water the products went into. Not asked for on a propagation, but kept when it is there.</summary>
    public decimal? WaterLitres { get; set; }

    /// <summary>Whether this kind of entry can carry a feed.</summary>
    public static bool TakesFeed(PropagationCareKind kind) =>
        kind is PropagationCareKind.Watered or PropagationCareKind.ChangedWater;

    public PropagationCareLog Copy()
    {
        var copy = (PropagationCareLog)MemberwiseClone();
        copy.Products = [.. Products.Select(p => p.Copy())];
        return copy;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (PropagationId == Guid.Empty)
            errors.Add(Strings.Propagations.ErrCareNeedsPropagation);

        if (Kind == PropagationCareKind.MoistureReading)
        {
            if (Moisture is null)
                errors.Add(Strings.Propagations.ErrReadingNeedsNumber);
            else if (Moisture is < 0 or > 10)
                errors.Add(Strings.Propagations.ErrReadingRange);
        }
        else if (Moisture is not null)
        {
            errors.Add(Strings.Propagations.ErrOnlyReadingHasNumber);
        }

        if ((Products.Count > 0 || FeedName is not null || WaterLitres is not null) && !TakesFeed(Kind))
            errors.Add(Strings.Propagations.ErrOnlyWateringHasFeed);

        if (WaterLitres <= 0)
            errors.Add(Strings.Care.LogWaterAmount);

        if (Products.Any(p => string.IsNullOrWhiteSpace(p.Name)))
            errors.Add(Strings.Care.LogProductNeedsName);

        if (Products.Any(p => p.Amount < 0))
            errors.Add(Strings.Care.LogDoseNegative);

        if (Products.Any(p => p.Per <= 0))
            errors.Add(Doses.NoWater);

        return errors;
    }
}
