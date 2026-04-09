namespace FlightService.Domain.Entities;

public class Flight
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty; // AI-101
    public string Airline { get; set; } = string.Empty;

    public int OriginAirportId { get; set; }
    public Airport? OriginAirport { get; set; }

    public int DestinationAirportId { get; set; }
    public Airport? DestinationAirport { get; set; }

    public int TotalEconomySeats { get; set; }
    public int TotalBusinessSeats { get; set; }

    public bool IsActive { get; set; } = true;
}