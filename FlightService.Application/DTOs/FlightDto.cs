namespace FlightService.Application.DTOs;

public class FlightDto
{
    public string FlightNumber { get; set; } = string.Empty;
    public string Airline { get; set; } = string.Empty;
    public int OriginAirportId { get; set; }
    public int DestinationAirportId { get; set; }
    public int TotalEconomySeats { get; set; }
    public int TotalBusinessSeats { get; set; }
}