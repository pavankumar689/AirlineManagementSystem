namespace PaymentService.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public int PassengerId { get; set; }
    public string PassengerEmail { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    // Card, UPI, NetBanking
    public string Status { get; set; } = "Pending";
    // Pending, Success, Failed, Refunded
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int ScheduleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }
}