using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Data;

namespace NotificationService.API.Controllers;

public class SubscribeDto
{
    public int ScheduleId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class AlertController : ControllerBase
{
    private readonly NotificationDbContext _db;

    public AlertController(NotificationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Allows a passenger to subscribe to email alerts for a specific flight schedule.
    /// The API Gateway intercepts this request, verifies the "Passenger" JWT role,
    /// and injects the User ID, Name, and Email into the headers.
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeDto dto)
    {
        var passengerId = int.Parse(Request.Headers["X-User-Id"].ToString());

        var existing = await _db.FlightAlerts
            .FirstOrDefaultAsync(a =>
                a.PassengerId == passengerId &&
                a.ScheduleId == dto.ScheduleId &&
                a.IsActive);

        if (existing != null)
            return BadRequest(new { message = "Already subscribed to this flight" });

        var alert = new FlightAlert
        {
            PassengerId = passengerId,
            PassengerEmail = Request.Headers["X-User-Email"].ToString(),
            PassengerName = Request.Headers["X-User-Name"].ToString(),
            ScheduleId = dto.ScheduleId,
            FlightNumber = dto.FlightNumber,
            Origin = dto.Origin,
            Destination = dto.Destination,
            DepartureTime = dto.DepartureTime
        };

        _db.FlightAlerts.Add(alert);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Successfully subscribed to flight alerts" });
    }

    /// <summary>
    /// Fetches all active flight alert subscriptions for the currently logged-in passenger.
    /// Used by the Passenger Portal to render the "My Alerts" dashboard panel.
    /// </summary>
    [HttpGet("my-alerts")]
    public async Task<IActionResult> GetMyAlerts()
    {
        var passengerId = int.Parse(Request.Headers["X-User-Id"].ToString());

        var alerts = await _db.FlightAlerts
            .Where(a => a.PassengerId == passengerId && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(alerts);
    }

    /// <summary>
    /// Allows a passenger to opt-out or unsubscribe from a flight alert.
    /// This performs a soft-delete (setting IsActive to false) rather than a hard delete,
    /// preserving historical audit logs of user preferences.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Unsubscribe(int id)
    {
        var passengerId = int.Parse(Request.Headers["X-User-Id"].ToString());

        var alert = await _db.FlightAlerts
            .FirstOrDefaultAsync(a => a.Id == id && a.PassengerId == passengerId);

        if (alert == null)
            return NotFound(new { message = "Alert not found" });

        alert.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Unsubscribed successfully" });
    }
}
