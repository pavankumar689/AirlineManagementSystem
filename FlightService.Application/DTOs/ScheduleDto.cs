namespace FlightService.Application.DTOs;

public class ScheduleDto
{
    public int FlightId { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal EconomyPrice { get; set; }
    public decimal BusinessPrice { get; set; }
}