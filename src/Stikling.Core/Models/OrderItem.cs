using System.Text.Json.Serialization;
using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// One plant or cutting on an <see cref="Order"/>, kept as a name and a price. When the box is
/// unpacked it becomes a plant or a propagation, and the item points at it. An existing plant can
/// be linked to an order too. Items are shown in the order they were made (CreatedAt).
/// </summary>
public sealed class OrderItem : Entity
{
    public Guid OrderId { get; set; }

    /// <summary>The name as typed.</summary>
    public string? Name { get; set; }

    /// <summary>Filled in when a suggestion was picked or the name was tidied.</summary>
    public string? Genus { get; set; }
    public string? Species { get; set; }
    public string? Cultivar { get; set; }

    /// <summary>
    /// What it cost, as a number with no currency. Once the item is a plant, the plant's own
    /// price is the one that counts.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>The wish it was bought for.</summary>
    public Guid? WishId { get; set; }

    /// <summary>The plant it became, or the existing plant it was linked to.</summary>
    public Guid? PlantId { get; set; }

    /// <summary>The propagation (cutting) it became.</summary>
    public Guid? PropagationId { get; set; }

    /// <summary>The day it was unpacked. Null while it is on the way, and for a linked plant.</summary>
    public DateOnly? ArrivedOn { get; set; }

    /// <summary>True when the box came without it.</summary>
    public bool DidNotCome { get; set; }

    /// <summary>Still waiting for the box: it hasn't become anything and wasn't marked as not coming.</summary>
    [JsonIgnore]
    public bool IsOnTheWay => PlantId is null && PropagationId is null && !DidNotCome;

    /// <summary>The botanical name, else the name typed, else a word saying there is none.</summary>
    [JsonIgnore]
    public string DisplayName =>
        PlantNames.Botanical(Genus, Species, Cultivar)
        ?? (string.IsNullOrWhiteSpace(Name) ? Strings.Orders.OrderItemUnnamed : Name.Trim());

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Genus))
            errors.Add(Strings.Orders.OrderItemNeedsName);
        if (Price < 0)
            errors.Add(Strings.Orders.OrderItemPriceNegative);
        return errors;
    }
}
