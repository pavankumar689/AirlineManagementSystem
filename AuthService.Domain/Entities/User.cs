namespace AuthService.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Passenger"; // Admin, Passenger
    public int RewardPoints { get; set; } = 0;       // Loyalty reward points balance
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}