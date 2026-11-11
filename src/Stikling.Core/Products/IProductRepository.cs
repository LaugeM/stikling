using Stikling.Core.Models;

namespace Stikling.Core.Products;

/// <summary>Care entries keep their own copy of a deleted product's name and dose.</summary>
public interface IProductRepository : IRepository<Product>
{
}
