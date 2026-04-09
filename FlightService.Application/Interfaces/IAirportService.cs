using FlightService.Application.DTOs;
using FlightService.Domain.Entities;

namespace FlightService.Application.Interfaces;

public interface IAirportService
{
    Task<List<Airport>> GetAllAsync();
    Task<Airport> CreateAsync(AirportDto dto);
    Task DeleteAsync(int id);
}