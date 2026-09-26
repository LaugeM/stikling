namespace Stikling.Core.Models;

/// <summary>
/// A feed you mix, saved under a name: "weekly aroid feed", "rooting water". Each product has its
/// own dose, and the order is the order they go in the water, since silica has to go
/// in first and be stirred before anything else. Nothing is ever sorted automatically.
///
/// A care entry copies the products and doses when a feed is logged, so a feed can be edited
/// freely without changing what was logged before.
/// </summary>
public sealed class Feed : Entity
{
    public string? Name { get; set; }

    public string? Notes { get; set; }

    /// <summary>What goes in the water, in the order it goes in.</summary>
    public List<ProductDose> Products { get; set; } = [];

    /// <summary>A copy with its own product list, so editing a draft can't change the original.</summary>
    public Feed Copy()
    {
        var copy = (Feed)MemberwiseClone();
        copy.Products = [.. Products.Select(p => p.Copy())];
        return copy;
    }

    public void MoveUp(int index)
    {
        if (index > 0 && index < Products.Count)
            (Products[index - 1], Products[index]) = (Products[index], Products[index - 1]);
    }

    public void MoveDown(int index)
    {
        if (index >= 0 && index < Products.Count - 1)
            (Products[index], Products[index + 1]) = (Products[index + 1], Products[index]);
    }

    /// <summary>
    /// The doses to log or measure out, each a copy. A product that still exists lends its
    /// current name, so renaming a bottle doesn't leave the feed saying the old one.
    /// </summary>
    public List<ProductDose> Doses(IEnumerable<Product> products)
    {
        var current = products.Where(p => !p.IsDeleted).ToDictionary(p => p.Id);

        return [.. Products.Select(dose =>
        {
            var copy = dose.Copy();
            if (dose.ProductId is { } id && current.TryGetValue(id, out var product) && !string.IsNullOrWhiteSpace(product.Name))
                copy.Name = product.Name.Trim();
            return copy;
        })];
    }

    /// <summary>
    /// A line when the order looks wrong: silica after something else, or pH before the end.
    /// Said rather than refused, the same way a soil mix that doesn't reach 100% is.
    /// </summary>
    public string? OrderNote(IEnumerable<Product> products)
    {
        var kinds = products.ToDictionary(p => p.Id, p => p.Kind);
        var order = Products
            .Select(dose => dose.ProductId is { } id && kinds.TryGetValue(id, out var kind) ? kind : (ProductKind?)null)
            .ToList();

        var lastSilica = order.LastIndexOf(ProductKind.Silica);
        if (lastSilica >= 0 && order.Take(lastSilica).Any(kind => kind != ProductKind.Silica))
            return "Silica usually goes in first and gets stirred in before anything else.";

        var firstPh = order.IndexOf(ProductKind.Ph);
        if (firstPh >= 0 && order.Skip(firstPh).Any(kind => kind != ProductKind.Ph))
            return "pH up or down usually goes in last, because everything else shifts the pH.";

        return null;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Give the feed a name.");

        if (Products.Count == 0)
            errors.Add("Add at least one product.");

        if (Products.Any(p => string.IsNullOrWhiteSpace(p.Name)))
            errors.Add("A product in the feed needs a name.");

        if (Products.Any(p => p.Amount < 0))
            errors.Add("A dose can't be less than 0.");

        if (Products.Any(p => p.Per <= 0))
            errors.Add(Models.Doses.NoWater);

        return errors;
    }
}
