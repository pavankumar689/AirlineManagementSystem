namespace Shared.Events;

public class PaymentCompletedEvent
{
    public int BookingId { get; set; }
    public int PassengerId { get; set; }
    public string PassengerEmail { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int ScheduleId { get; set; }
    public string Class { get; set; } = string.Empty;
}