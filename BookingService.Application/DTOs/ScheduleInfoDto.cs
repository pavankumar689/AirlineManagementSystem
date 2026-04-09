namespace BookingService.Application.DTOs;

public class ScheduleInfoDto
{
    public int Id { get; set; }
    public int FlightId { get; set; }
    public FlightInfoDto? Flight { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal EconomyPrice { get; set; }
    public decimal BusinessPrice { get; set; }
    public int AvailableEconomySeats { get; set; }
    public int AvailableBusinessSeats { get; set; }
    public string Status { get; set; } = string.Empty;

    // Helper properties to get nested values
    public string FlightNumber => Flight?.FlightNumber ?? string.Empty;
    public string OriginCode => Flight?.OriginAirport?.Code ?? string.Empty;
    public string DestinationCode => Flight?.DestinationAirport?.Code ?? string.Empty;
}

public class FlightInfoDto
{
    public int Id { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Airline { get; set; } = string.Empty;
    public AirportInfoDto? OriginAirport { get; set; }
    public AirportInfoDto? DestinationAirport { get; set; }
}

public class AirportInfoDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}