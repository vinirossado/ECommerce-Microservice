using Notification.Models;

namespace Notification.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendOrderConfirmationAsync(int userId, int orderId, decimal totalAmount);
    Task SendOrderStatusUpdateAsync(int userId, int orderId, string status);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // In a production environment, this would integrate with a real email service
        // like SendGrid, AWS SES, etc. For now, we'll just log the email
        _logger.LogInformation("Sending email: To={To}, Subject={Subject}, Body={Body}", to, subject, body);
        
        // Simulate email sending delay
        await Task.Delay(100);
        
        // Log success
        _logger.LogInformation("Email sent successfully to {To}", to);
    }

    public async Task SendOrderConfirmationAsync(int userId, int orderId, decimal totalAmount)
    {
        // In a real implementation, you would fetch user email from User Service
        string userEmail = $"user{userId}@example.com"; // Placeholder
        
        string subject = $"Order Confirmation #{orderId}";
        string body = $@"
            Dear Customer,

            Thank you for your order. Your order #{orderId} has been received and is being processed.

            Order Total: ${totalAmount}

            We will notify you when your order ships.

            Thank you for shopping with us!
            E-Commerce Team
        ";
        
        await SendEmailAsync(userEmail, subject, body);
    }

    public async Task SendOrderStatusUpdateAsync(int userId, int orderId, string status)
    {
        // In a real implementation, you would fetch user email from User Service
        string userEmail = $"user{userId}@example.com"; // Placeholder
        
        string subject = $"Order #{orderId} Status Update";
        string body = $@"
            Dear Customer,

            Your order #{orderId} has been updated to: {status}.

            {GetStatusSpecificMessage(status)}

            Thank you for shopping with us!
            E-Commerce Team
        ";
        
        await SendEmailAsync(userEmail, subject, body);
    }
    
    private string GetStatusSpecificMessage(string status)
    {
        return status switch
        {
            "Shipped" => "Your order has been shipped and is on its way to you. You can track your shipment using the tracking number provided in your account.",
            "Delivered" => "Your order has been delivered. We hope you enjoy your purchase!",
            "Cancelled" => "Your order has been cancelled. If you have any questions, please contact our customer service.",
            _ => string.Empty
        };
    }
}
