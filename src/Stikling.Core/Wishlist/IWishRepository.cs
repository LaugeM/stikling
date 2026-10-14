using Stikling.Core.Models;

namespace Stikling.Core.Wishlist;

public interface IWishRepository
{
    /// <summary>All wishes that aren't deleted, got ones included.</summary>
    Task<IReadOnlyList<Wish>> GetAllAsync();

    Task<Wish?> GetAsync(Guid id);

    /// <summary>Adds or updates a wish. Throws if it isn't valid.</summary>
    Task SaveAsync(Wish wish);

    /// <summary>Soft-deletes a wish.</summary>
    Task DeleteAsync(Guid id);
}
