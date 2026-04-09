namespace BookingService.Application.DTOs;

public class BookingResponseDto
{
    public int BookingId { get; set; }
    public string PNR { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string PassengerEmail { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public string Class { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}