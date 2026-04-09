using FlightService.Application.DTOs;
using FlightService.Application.Interfaces;
using FlightService.Domain.Entities;
using FlightService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightService.Infrastructure.Services;

public class AirportServiceImpl : IAirportService
{
    private readonly FlightDbContext _db;

    public AirportServiceImpl(FlightDbContext db)
    {
        _db = db;
    }

    public async Task<List<Airport>> GetAllAsync()
    {
        return await _db.Airports.ToListAsync();
    }

    public async Task<Airport> CreateAsync(AirportDto dto)
    {
        var airport = new Airport
        {
            Name = dto.Name,
            Code = dto.Code.ToUpper(),
            City = dto.City,
            Country = dto.Country
        };

        _db.Airports.Add(airport);
        await _db.SaveChangesAsync();
        return airport;
    }

    public async Task DeleteAsync(int id)
    {
        var airport = await _db.Airports.FindAsync(id);
        if (airport == null)
            throw new Exception("Airport not found");

        _db.Airports.Remove(airport);
        await _db.SaveChangesAsync();
    }
}