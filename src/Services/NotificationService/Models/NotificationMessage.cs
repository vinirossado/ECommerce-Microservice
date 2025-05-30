namespace Notification.Models;

public class NotificationMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = "Email"; // Email, SMS, Push
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}
