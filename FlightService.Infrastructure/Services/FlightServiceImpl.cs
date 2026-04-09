using FlightService.Application.DTOs;
using FlightService.Application.Interfaces;
using FlightService.Domain.Entities;
using FlightService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlightService.Infrastructure.Services;

public class FlightServiceImpl : IFlightService
{
    private readonly FlightDbContext _db;

    public FlightServiceImpl(FlightDbContext db)
    {
        _db = db;
    }

    public async Task<List<Flight>> GetAllAsync()
    {
        return await _db.Flights
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .ToListAsync();
    }

    public async Task<Flight> CreateAsync(FlightDto dto)
    {
        var flight = new Flight
        {
            FlightNumber = dto.FlightNumber,
            Airline = dto.Airline,
            OriginAirportId = dto.OriginAirportId,
            DestinationAirportId = dto.DestinationAirportId,
            TotalEconomySeats = dto.TotalEconomySeats,
            TotalBusinessSeats = dto.TotalBusinessSeats
        };

        _db.Flights.Add(flight);
        await _db.SaveChangesAsync();

        // Reload with airport data
        return await _db.Flights
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .FirstAsync(f => f.Id == flight.Id);
    }

    public async Task<Flight> UpdateAsync(int id, FlightDto dto)
    {
        var flight = await _db.Flights.FindAsync(id);
        if (flight == null)
            throw new Exception("Flight not found");

        flight.FlightNumber = dto.FlightNumber;
        flight.Airline = dto.Airline;
        flight.OriginAirportId = dto.OriginAirportId;
        flight.DestinationAirportId = dto.DestinationAirportId;
        flight.TotalEconomySeats = dto.TotalEconomySeats;
        flight.TotalBusinessSeats = dto.TotalBusinessSeats;

        await _db.SaveChangesAsync();
        return flight;
    }

    public async Task DeleteAsync(int id)
    {
        var flight = await _db.Flights.FindAsync(id);
        if (flight == null)
            throw new Exception("Flight not found");

        _db.Flights.Remove(flight);
        await _db.SaveChangesAsync();
    }
}