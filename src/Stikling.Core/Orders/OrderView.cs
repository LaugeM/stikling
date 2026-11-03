using Stikling.Core.Models;

namespace Stikling.Core.Orders;

/// <summary>
/// An order with its items and what is worked out from them. Nothing here is stored: it is read
/// from the items each time, so two devices can't disagree about whether a box has arrived.
/// </summary>
public sealed record OrderView
{
    public required Order Order { get; init; }

    /// <summary>The items that aren't deleted, in the order they were made.</summary>
    public required IReadOnlyList<OrderItem> Items { get; init; }

    /// <summary>The seller's id, following a merge. Null when the seller is gone or only a name was typed.</summary>
    public Guid? SellerId { get; init; }

    /// <summary>The seller's name, following a merge, else the name saved on the order.</summary>
    public string? SellerName { get; init; }

    /// <summary>The price of each item with one, by item id. A plant item shows its plant's price.</summary>
    public IReadOnlyDictionary<Guid, decimal> Prices { get; init; } = new Dictionary<Guid, decimal>();

    public bool IsOnTheWay => Items.Any(i => i.IsOnTheWay);

    /// <summary>The latest day an item was unpacked, once nothing is on the way any more.</summary>
    public DateOnly? ArrivedOn =>
        IsOnTheWay ? null : Items.Where(i => i.ArrivedOn is not null).Select(i => i.ArrivedOn).Max();

    public int ItemCount => Items.Count;

    /// <summary>Items that became a plant or a cutting, or were linked to a plant.</summary>
    public int CameCount => Items.Count(i => i.PlantId is not null || i.PropagationId is not null);

    private IEnumerable<OrderItem> Counted => Items.Where(i => !i.DidNotCome);

    /// <summary>The price shown for an item, or null when it has none.</summary>
    public decimal? PriceOf(OrderItem item) => Prices.TryGetValue(item.Id, out var price) ? price : null;

    /// <summary>The prices of the items that didn't fail to come.</summary>
    public decimal ItemsTotal => Counted.Sum(i => PriceOf(i) ?? 0);

    public decimal Total => ItemsTotal + (Order.Shipping ?? 0);

    /// <summary>Shipping divided equally among the items that didn't fail to come. Null with no shipping or no such items.</summary>
    public decimal? ShippingEach
    {
        get
        {
            var count = Counted.Count();
            return Order.Shipping is { } shipping && shipping > 0 && count > 0 ? shipping / count : null;
        }
    }
}

/// <summary>An order that is on the way and expected, for Today.</summary>
public sealed record OrderArriving(Guid OrderId, string? SellerName, DateOnly ExpectedOn, bool IsToday);

/// <summary>An open wish that is on an order on the way.</summary>
public sealed record WishOnOrder(Guid OrderId, string? SellerName);

public enum UnpackAs
{
    Plant,
    Cutting
}

/// <summary>What to do with one item when a box is unpacked. Nothing here is stored.</summary>
public sealed class UnpackChoice
{
    public Guid ItemId { get; set; }
    public UnpackAs UnpackAs { get; set; } = UnpackAs.Plant;
    public ArrivalRating? Rating { get; set; }
    public List<ArrivalProblem> Problems { get; set; } = [];
    public string? ArrivalNote { get; set; }

    /// <summary>A tidied name. When any of the four is filled in, they replace the item's.</summary>
    public string? Genus { get; set; }
    public string? Species { get; set; }
    public string? Cultivar { get; set; }
    public string? Name { get; set; }
}

/// <summary>The plants and propagations an unpack made.</summary>
public sealed record UnpackResult(IReadOnlyList<Guid> PlantIds, IReadOnlyList<Guid> PropagationIds);
