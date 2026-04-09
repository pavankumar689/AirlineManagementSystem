using static System.Net.WebRequestMethods;

namespace FlightService.Domain.Entities;

public class Schedule
{
    public int Id { get; set; }

    public int FlightId { get; set; }
    public Flight? Flight { get; set; }

    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }

    public decimal EconomyPrice { get; set; }
    public decimal BusinessPrice { get; set; }

    public int AvailableEconomySeats { get; set; }
    public int AvailableBusinessSeats { get; set; }

    public string Status { get; set; } = "Scheduled"; // Scheduled, Delayed, Cancelled
}
