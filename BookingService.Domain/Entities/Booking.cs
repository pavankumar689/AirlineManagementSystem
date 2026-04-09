namespace BookingService.Domain.Entities;

public class Booking
{
    public int Id { get; set; }
    public int PassengerId { get; set; }
    public string PassengerName { get; set; } = string.Empty;
    public string PassengerEmail { get; set; } = string.Empty;
    public int ScheduleId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public string Class { get; set; } = "Economy";
    public string SeatNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    // Pending → PaymentProcessing → Confirmed → Cancelled
    public string PNR { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Payment? Payment { get; set; }
}