namespace BookingService.Application.DTOs;

public class PassengerRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
}

public class CreateBookingDto
{
    public int ScheduleId { get; set; }
    public string Class { get; set; } = "Economy";
    public string PaymentMethod { get; set; } = string.Empty;
    public List<PassengerRequestDto> Passengers { get; set; } = new();
}