using FlightService.Application.DTOs;
using FlightService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FlightService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly IScheduleService _scheduleService;

    public ScheduleController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    // Public
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var schedules = await _scheduleService.GetAllAsync();
        return Ok(schedules);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] FlightSearchDto dto)
    {
        var schedules = await _scheduleService.SearchAsync(dto);
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var schedule = await _scheduleService.GetByIdAsync(id);
        if (schedule == null)
            return NotFound(new { message = "Schedule not found" });
        return Ok(schedule);
    }

    // Admin only — gateway enforces Admin/SuperAdmin role
    [HttpPost]
    public async Task<IActionResult> Create(ScheduleDto dto)
    {
        try
        {
            var schedule = await _scheduleService.CreateAsync(dto);
            return Ok(schedule);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
    {
        try
        {
            await _scheduleService.UpdateStatusAsync(id, status);
            return Ok(new { message = "Status updated" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Internal — called by BookingService
    [HttpPatch("{id}/deduct-seat")]
    public async Task<IActionResult> DeductSeat(int id, [FromQuery] string seatClass)
    {
        var result = await _scheduleService.DeductSeatAsync(id, seatClass);
        if (!result)
            return BadRequest(new { message = "No seats available" });
        return Ok(new { message = "Seat deducted" });
    }

    [HttpPatch("{id}/release-seat")]
    public async Task<IActionResult> ReleaseSeat(int id, [FromQuery] string seatClass)
    {
        var result = await _scheduleService.ReleaseSeatAsync(id, seatClass);
        if (!result)
            return BadRequest(new { message = "Failed to release seat" });
        return Ok(new { message = "Seat released" });
    }
}
