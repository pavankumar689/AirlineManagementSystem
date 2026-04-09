using FlightService.Application.DTOs;
using FlightService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FlightService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightController : ControllerBase
{
    private readonly IFlightService _flightService;

    public FlightController(IFlightService flightService)
    {
        _flightService = flightService;
    }

    /// <summary>
    /// Retrieves a list of all flights inside the system.
    /// Publicly accessible so that users can search flights without being logged in.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var flights = await _flightService.GetAllAsync();
        return Ok(flights);
    }

    /// <summary>
    /// Creates a new flight record in the database.
    /// Endpoint is guarded by the API Gateway to only allow users with Admin/SuperAdmin roles.
    /// </summary>
    /// <param name="dto">Data transfer object containing flight details.</param>
    [HttpPost]
    public async Task<IActionResult> Create(FlightDto dto)
    {
        try
        {
            var flight = await _flightService.CreateAsync(dto);
            return Ok(flight);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing flight schedule or configuration.
    /// </summary>
    /// <param name="id">The flight ID.</param>
    /// <param name="dto">The updated data.</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, FlightDto dto)
    {
        try
        {
            var flight = await _flightService.UpdateAsync(id, dto);
            return Ok(flight);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a flight from the system permanently.
    /// </summary>
    /// <param name="id">The flight ID.</param>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _flightService.DeleteAsync(id);
            return Ok(new { message = "Flight deleted" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
