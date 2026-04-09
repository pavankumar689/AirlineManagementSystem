using FlightService.Application.DTOs;
using FlightService.Domain.Entities;

namespace FlightService.Application.Interfaces;

public interface IFlightService
{
    Task<List<Flight>> GetAllAsync();
    Task<Flight> CreateAsync(FlightDto dto);
    Task<Flight> UpdateAsync(int id, FlightDto dto);
    Task DeleteAsync(int id);
}