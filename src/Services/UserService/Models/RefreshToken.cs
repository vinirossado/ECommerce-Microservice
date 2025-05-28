using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace User.Models;

public class RefreshToken
{
    [Key]
    public int Id { get; init; }
    
    [Required]
    public string Token { get; init; } = string.Empty;
    
    [Required]
    public DateTime ExpiryDate { get; init; }
    
    [Required]
    public int UserId { get; init; }
    
    [ForeignKey("UserId")]
    public User User { get; init; } = null!;
    
    public bool IsExpired => DateTime.UtcNow >= ExpiryDate;
    
    public bool IsActive { get; set; } = true;
    
    public string? ReplacedByToken { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    public string CreatedByIp { get; set; } = string.Empty;
    
    public DateTime? RevokedAt { get; set; }
    
    public string? RevokedByIp { get; set; }
    
    public string? ReasonRevoked { get; set; }
}
