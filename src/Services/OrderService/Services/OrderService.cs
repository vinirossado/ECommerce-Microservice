using Microsoft.EntityFrameworkCore;
using Order.Data;
using Order.Models;

namespace Order.Services;

public class OrderService : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Models.Order>> GetAllOrdersAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<Models.Order?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
    }

    public async Task<IEnumerable<Models.Order>> GetUserOrdersAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Models.Order> CreateOrderAsync(Models.Order order, CancellationToken cancellationToken = default)
    {
        // Calculate total amount based on order items
        order.TotalAmount = order.OrderItems.Sum(item => item.Quantity * item.UnitPrice);
        
        await _context.Orders.AddAsync(order, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Created new order with ID {OrderId} for user {UserId}", order.Id, order.UserId);
        
        return order;
    }

    public async Task<Models.Order?> UpdateOrderStatusAsync(int orderId, string status, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders.FindAsync(new object[] { orderId }, cancellationToken);
        
        if (order == null)
        {
            return null;
        }

        order.Status = status;
        
        // Update additional fields based on status
        switch (status)
        {
            case "Shipped":
                order.ShippedDate = DateTime.UtcNow;
                break;
            case "Delivered":
                order.DeliveredDate = DateTime.UtcNow;
                break;
        }
        
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Updated order {OrderId} status to {Status}", orderId, status);
        
        return order;
    }

    public async Task<bool> DeleteOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders.FindAsync(new object[] { orderId }, cancellationToken);
        
        if (order == null)
        {
            return false;
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Deleted order with ID {OrderId}", orderId);
        
        return true;
    }
}
