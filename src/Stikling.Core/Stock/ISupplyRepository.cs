using Stikling.Core.Models;

namespace Stikling.Core.Stock;

public interface ISupplyRepository
{
    /// <summary>All supplies that aren't deleted.</summary>
    Task<IReadOnlyList<Supply>> GetAllAsync();

    Task<Supply?> GetAsync(Guid id);

    /// <summary>Adds or updates a supply. Throws if it isn't valid.</summary>
    Task SaveAsync(Supply supply);

    /// <summary>Soft-deletes the supply. Its entries stay, and are ignored while it is gone.</summary>
    Task DeleteAsync(Guid id);
}

public interface ISupplyEntryRepository
{
    /// <summary>All entries that aren't deleted.</summary>
    Task<IReadOnlyList<SupplyEntry>> GetAllAsync();

    Task<SupplyEntry?> GetAsync(Guid id);

    /// <summary>Adds or updates an entry. Throws if it isn't valid.</summary>
    Task SaveAsync(SupplyEntry entry);

    /// <summary>Soft-deletes the entry.</summary>
    Task DeleteAsync(Guid id);
}

public interface IShoppingItemRepository
{
    /// <summary>All lines that aren't deleted.</summary>
    Task<IReadOnlyList<ShoppingItem>> GetAllAsync();

    Task<ShoppingItem?> GetAsync(Guid id);

    /// <summary>Adds or updates a line. Throws if it isn't valid.</summary>
    Task SaveAsync(ShoppingItem item);

    /// <summary>Soft-deletes the line.</summary>
    Task DeleteAsync(Guid id);
}
