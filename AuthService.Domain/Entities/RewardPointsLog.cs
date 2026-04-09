namespace AuthService.Domain.Entities;

public class RewardPointsLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int Points { get; set; }  // Positive = earned, Negative = redeemed
    public string Type { get; set; } = string.Empty; // "Earned" | "Redeemed" | "Refunded"
    public string Description { get; set; } = string.Empty;
    public string? ReferenceId { get; set; } // BookingId or PNR
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
