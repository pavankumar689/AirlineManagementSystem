namespace NotificationService.Domain.Entities;

public class FlightAlert
{
    public int Id { get; set; }
    public int PassengerId { get; set; }
    public string PassengerEmail { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public int ScheduleId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}