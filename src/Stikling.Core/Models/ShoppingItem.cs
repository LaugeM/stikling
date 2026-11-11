using Strings = Stikling.Core.Resources;

namespace Stikling.Core.Models;

/// <summary>
/// A loose line on the shopping list: something typed, or a pot put on the list. Supplies that are
/// running low or out are on the list without a line of their own.
/// </summary>
public sealed class ShoppingItem : Entity
{
    public string Text { get; set; } = "";

    /// <summary>
    /// Set for a pot line. <see cref="Text"/> is then the pot's name at the time, which is shown when
    /// the pot is gone.
    /// </summary>
    public Guid? PotId { get; set; }

    /// <summary>When it was ticked off. Null while it still has to be bought.</summary>
    public DateTimeOffset? BoughtAt { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Price { get; set; }

    public Guid? SellerId { get; set; }

    public ShoppingItem Copy() => (ShoppingItem)MemberwiseClone();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Text))
            errors.Add(Strings.Stock.ShoppingNeedsText);
        if (Amount < 0)
            errors.Add(Strings.Stock.AmountNegative);
        if (Price < 0)
            errors.Add(Strings.Stock.PriceNegative);
        return errors;
    }
}
