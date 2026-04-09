namespace Shared.Events;

public class FlightStatusChangedEvent
{
    public int ScheduleId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
}
