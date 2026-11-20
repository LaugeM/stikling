using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>What happened to a supply. An unknown name reads as null and is ignored when working out the level.</summary>
public enum SupplyEntryKind
{
    Bought,
    RunningLow,
    Out,

    /// <summary>Counted what is left.</summary>
    Counted,

    /// <summary>
    /// Used up some: mixing a batch of soil or a can of feed took it off. An older version of the app
    /// doesn't know the name, so it ignores these and shows a higher level until it updates.
    /// </summary>
    Used
}

/// <summary>
/// One change to a supply's stock, with the day it happened. The level and the amount are worked out
/// from the entries (see <c>SupplyStatus</c>), so two devices adding entries never overwrite each other.
/// </summary>
public sealed class SupplyEntry : Entity
{
    public Guid SupplyId { get; set; }

    public DateTimeOffset At { get; set; }

    [JsonConverter(typeof(TolerantNullableEnumConverter<SupplyEntryKind>))]
    public SupplyEntryKind? Kind { get; set; }

    /// <summary>In the supply's unit. For Bought how much was bought, for Counted how much is left.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Bought only.</summary>
    public decimal? Price { get; set; }

    /// <summary>Bought only.</summary>
    public Guid? SellerId { get; set; }

    /// <summary>
    /// Used only: the batch mixing or can of feed it was taken off for. When that record is deleted, the
    /// entry is deleted with it and the level goes back up.
    /// </summary>
    public Guid? UsedForId { get; set; }

    public SupplyEntry Copy() => (SupplyEntry)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Amount < 0)
            errors.Add(Strings.Stock.AmountNegative);
        if (Price < 0)
            errors.Add(Strings.Stock.PriceNegative);
        return errors;
    }
}
