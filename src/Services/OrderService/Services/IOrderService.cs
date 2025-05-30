using Order.Models;

namespace Order.Services;

public interface IOrderService
{
    Task<IEnumerable<Models.Order>> GetAllOrdersAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<Models.Order?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Models.Order>> GetUserOrdersAsync(int userId, CancellationToken cancellationToken = default);
    Task<Models.Order> CreateOrderAsync(Models.Order order, CancellationToken cancellationToken = default);
    Task<Models.Order?> UpdateOrderStatusAsync(int orderId, string status, CancellationToken cancellationToken = default);
    Task<bool> DeleteOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
