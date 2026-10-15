using Stikling.Core.Models;

namespace Stikling.Core.Sellers;

public interface ISellerRepository
{
    /// <summary>
    /// Every seller stored, with the merged and deleted ones kept so that whatever still points at
    /// them can be followed to where they went.
    /// </summary>
    Task<SellerList> GetSellersAsync();

    /// <summary>Adds or updates a seller. Throws if it isn't valid.</summary>
    Task SaveAsync(Seller seller);

    /// <summary>Soft-deletes a seller.</summary>
    Task DeleteAsync(Guid id);
}
