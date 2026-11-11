using Stikling.Core.Models;

namespace Stikling.Core.Stock;

/// <summary>A deleted supply's entries stay, and are ignored while it is gone.</summary>
public interface ISupplyRepository : IRepository<Supply>
{
}

public interface ISupplyEntryRepository : IRepository<SupplyEntry>
{
}

public interface IShoppingItemRepository : IRepository<ShoppingItem>
{
}
