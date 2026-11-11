using Stikling.Core.Models;

namespace Stikling.Core.Orders;

public interface IOrderRepository : IRepository<Order>
{
}

/// <summary>GetAllAsync returns the items of every order.</summary>
public interface IOrderItemRepository : IRepository<OrderItem>
{
}
