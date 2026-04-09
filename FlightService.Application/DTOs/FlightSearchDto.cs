namespace FlightService.Application.DTOs;

// This is what passenger sends to search flights
public class FlightSearchDto
{
    public string OriginCode { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public DateTime TravelDate { get; set; }
    public string Class { get; set; } = "Economy";
}