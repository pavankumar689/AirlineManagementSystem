namespace Shared.Events;

public class BookingCancelledEvent
{
    public int BookingId { get; set; }
    public string PassengerEmail { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
}