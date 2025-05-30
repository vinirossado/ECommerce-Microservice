using Microsoft.AspNetCore.Mvc;
using Order.Services;

namespace Order.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Models.Order>>> GetOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var orders = await _orderService.GetAllOrdersAsync(page, pageSize);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders");
            return StatusCode(500, "An error occurred while retrieving orders");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Models.Order>> GetOrder(int id)
    {
        try
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            
            if (order == null)
            {
                return NotFound($"Order with ID {id} not found");
            }
            
            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order with ID {OrderId}", id);
            return StatusCode(500, "An error occurred while retrieving the order");
        }
    }

    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<IEnumerable<Models.Order>>> GetUserOrders(int userId)
    {
        try
        {
            var orders = await _orderService.GetUserOrdersAsync(userId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for user ID {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving user orders");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Models.Order>> CreateOrder([FromBody] Models.Order order)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        
        try
        {
            var createdOrder = await _orderService.CreateOrderAsync(order);
            return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, "An error occurred while creating the order");
        }
    }

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<Models.Order>> UpdateOrderStatus(int id, [FromBody] string status)
    {
        try
        {
            var updatedOrder = await _orderService.UpdateOrderStatusAsync(id, status);
            
            if (updatedOrder == null)
            {
                return NotFound($"Order with ID {id} not found");
            }
            
            return Ok(updatedOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for order with ID {OrderId}", id);
            return StatusCode(500, "An error occurred while updating the order status");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteOrder(int id)
    {
        try
        {
            var result = await _orderService.DeleteOrderAsync(id);
            
            if (!result)
            {
                return NotFound($"Order with ID {id} not found");
            }
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting order with ID {OrderId}", id);
            return StatusCode(500, "An error occurred while deleting the order");
        }
    }
}
