using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>Where pollen is kept.</summary>
public enum PollenStorage
{
    Freezer,
    Fridge,
    Room
}

/// <summary>
/// Pollen kept for a cross: from a plant in the app or a name typed in, collected on a day, kept in
/// the freezer, the fridge or at room temperature. The app shows how long ago it was collected and where
/// it is kept and says nothing about how long it lasts. The genus is kept on the record, so pollen from
/// a plant that is gone can still be matched to a plant coming into flower.
/// </summary>
public sealed class Pollen : Entity
{
    public const int MaxAmountLength = 80;

    /// <summary>The plant it was collected from, when it is one in the app.</summary>
    public Guid? FromPlantId { get; set; }

    /// <summary>The plant's name kept beside its id, or the name typed in for a plant that isn't in the app.</summary>
    public string? FromName { get; set; }

    public string? Genus { get; set; }

    public string? Species { get; set; }

    public DateOnly CollectedOn { get; set; }

    [JsonConverter(typeof(TolerantNullableEnumConverter<PollenStorage>))]
    public PollenStorage? KeptIn { get; set; }

    /// <summary>A rough amount in the person's own words, like "a small tube" or "6 anthers".</summary>
    public string? Amount { get; set; }

    public string? Note { get; set; }

    /// <summary>The day it was all used or thrown away. Null while some is left.</summary>
    public DateOnly? UsedUpOn { get; set; }

    [JsonIgnore]
    public bool IsUsedUp => UsedUpOn is not null;

    public Pollen Copy() => (Pollen)MemberwiseClone();

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (FromPlantId is null && string.IsNullOrWhiteSpace(FromName))
            errors.Add(Strings.Crosses.ErrPollenNeedsPlant);
        if (CollectedOn > today)
            errors.Add(Strings.Crosses.ErrFuture);
        if (UsedUpOn is { } used && used < CollectedOn)
            errors.Add(Strings.Crosses.ErrUsedBeforeCollected);
        if (Amount is { Length: > MaxAmountLength })
            errors.Add(Strings.Crosses.ErrAmountTooLong);
        return errors;
    }
}
