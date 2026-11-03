using Stikling.Core.Models;

namespace Stikling.Core.Orders;

public interface IOrderRepository
{
    /// <summary>All orders that aren't deleted.</summary>
    Task<IReadOnlyList<Order>> GetAllAsync();

    Task<Order?> GetAsync(Guid id);

    /// <summary>Adds or updates an order. Throws if it isn't valid.</summary>
    Task SaveAsync(Order order);

    /// <summary>Soft-deletes an order.</summary>
    Task DeleteAsync(Guid id);
}

public interface IOrderItemRepository
{
    /// <summary>All items that aren't deleted, whatever order they are on.</summary>
    Task<IReadOnlyList<OrderItem>> GetAllAsync();

    Task<OrderItem?> GetAsync(Guid id);

    /// <summary>Adds or updates an item. Throws if it isn't valid.</summary>
    Task SaveAsync(OrderItem item);

    /// <summary>Soft-deletes an item.</summary>
    Task DeleteAsync(Guid id);
}
