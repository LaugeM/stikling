using Stikling.Core.Models;

namespace Stikling.Core.Products;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();

    Task<Product?> GetAsync(Guid id);

    Task SaveAsync(Product product);

    /// <summary>Soft-deletes the product. Care entries keep their own copy of its name and dose.</summary>
    Task DeleteAsync(Guid id);
}
