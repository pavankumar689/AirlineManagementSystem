using FlightService.Application.DTOs;
using FlightService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FlightService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AirportController : ControllerBase
{
    private readonly IAirportService _airportService;

    public AirportController(IAirportService airportService)
    {
        _airportService = airportService;
    }

    // Public
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var airports = await _airportService.GetAllAsync();
        return Ok(airports);
    }

    // Admin only — gateway enforces Admin/SuperAdmin role
    [HttpPost]
    public async Task<IActionResult> Create(AirportDto dto)
    {
        try
        {
            var airport = await _airportService.CreateAsync(dto);
            return Ok(airport);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _airportService.DeleteAsync(id);
            return Ok(new { message = "Airport deleted" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
