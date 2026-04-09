namespace AuthService.Domain.Entities;

public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;       // Secure random token (stored hashed)
    public DateTime ExpiryDate { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
