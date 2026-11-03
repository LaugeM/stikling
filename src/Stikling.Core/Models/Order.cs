using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// Plants bought online and on the way. It records the seller, the day, an expected day, the shipping
/// and the items, which are <see cref="OrderItem"/>s of their own. Whether it is on the way or has
/// arrived is not stored: it is worked out from the items, so two devices can't disagree.
/// </summary>
public sealed class Order : Entity
{
    /// <summary>The seller, followed through a merge. Null when only the name was typed.</summary>
    public Guid? SellerId { get; set; }

    /// <summary>The seller's name as text beside the id, the way a plant keeps its Source.</summary>
    public string? SellerName { get; set; }

    public DateOnly OrderedOn { get; set; }

    /// <summary>The day the box is expected. Null when it isn't known.</summary>
    public DateOnly? ExpectedOn { get; set; }

    /// <summary>
    /// What shipping cost, as a number with no currency, the same way as <see cref="Plant.PricePaid"/>.
    /// It is never put on the plants.
    /// </summary>
    public decimal? Shipping { get; set; }

    public string? Notes { get; set; }

    public IReadOnlyList<string> Validate(DateOnly today)
    {
        var errors = new List<string>();
        if (SellerId is null && string.IsNullOrWhiteSpace(SellerName))
            errors.Add(Strings.Orders.OrderNeedsSeller);
        if (Shipping < 0)
            errors.Add(Strings.Orders.OrderShippingNegative);
        if (ExpectedOn < OrderedOn)
            errors.Add(Strings.Orders.OrderExpectedBeforeOrdered);
        return errors;
    }
}
