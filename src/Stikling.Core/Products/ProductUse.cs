using Stikling.Core.Care;
using Stikling.Core.Models;

namespace Stikling.Core.Products;

/// <summary>A product, how many plants have had it, and when it was last used.</summary>
public sealed record ProductUse(Product Product, int Plants, DateOnly? LastUsed)
{
    /// <summary>
    /// Every product with its use counted from the care log. Plants rather than entries
    /// are counted, because one feed logged for ten plants is ten entries.
    /// </summary>
    public static IReadOnlyList<ProductUse> List(IEnumerable<Product> products, IEnumerable<CareLog> logs)
    {
        var doses = logs
            .Where(l => !l.IsDeleted)
            .SelectMany(l => l.Products.Select(p => (p.ProductId, l.PlantId, l.OccurredOn)))
            .ToList();

        return products
            .Where(p => !p.IsDeleted)
            .Select(product =>
            {
                var used = doses.Where(d => d.ProductId == product.Id).ToList();
                return new ProductUse(
                    product,
                    used.Select(d => d.PlantId).Distinct().Count(),
                    used.Count == 0 ? null : used.Max(d => d.OccurredOn));
            })
            .OrderBy(use => use.Product.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

/// <summary>The products, with their use read from the care log.</summary>
public sealed class ProductService(IProductRepository products, ICareLogRepository logs)
{
    public async Task<IReadOnlyList<ProductUse>> GetAllAsync() =>
        ProductUse.List(await products.GetAllAsync(), await logs.GetAllAsync());
}
