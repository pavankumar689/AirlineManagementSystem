namespace NotificationService.Domain.Entities;

public class NotificationLog
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    // BookingConfirmed, BookingCancelled
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}