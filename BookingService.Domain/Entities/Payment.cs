namespace BookingService.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    // Card, UPI, NetBanking
    public string Status { get; set; } = "Pending";
    // Pending, Success, Failed
    public DateTime? PaidAt { get; set; }
}