using FlightService.Application.DTOs;
using FlightService.Application.Interfaces;
using FlightService.Domain.Entities;
using FlightService.Infrastructure.Data;
using FlightService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Shared.Events;

namespace FlightService.Infrastructure.Services;

public class ScheduleServiceImpl : IScheduleService
{
    private readonly FlightDbContext _db;
    private readonly RabbitMQPublisher _publisher;

    public ScheduleServiceImpl(FlightDbContext db, RabbitMQPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<List<Schedule>> GetAllAsync()
    {
        return await _db.Schedules
            .Include(s => s.Flight)
                .ThenInclude(f => f!.OriginAirport)
            .Include(s => s.Flight)
                .ThenInclude(f => f!.DestinationAirport)
            .OrderByDescending(s => s.DepartureTime)
            .ToListAsync();
    }

    public async Task<List<Schedule>> SearchAsync(FlightSearchDto dto)
    {
        return await _db.Schedules
            .Include(s => s.Flight)
                .ThenInclude(f => f!.OriginAirport)
            .Include(s => s.Flight)
                .ThenInclude(f => f!.DestinationAirport)
            .Where(s =>
                s.Flight!.OriginAirport!.Code == dto.OriginCode.ToUpper() &&
                s.Flight!.DestinationAirport!.Code == dto.DestinationCode.ToUpper() &&
                s.DepartureTime.Date == dto.TravelDate.Date &&
                s.Status == "Scheduled" &&
                (dto.Class == "Economy" ? s.AvailableEconomySeats > 0 : s.AvailableBusinessSeats > 0)
            )
            .ToListAsync();
    }
    public async Task<bool> DeductSeatAsync(int scheduleId, string seatClass)
    {
        var schedule = await _db.Schedules.FindAsync(scheduleId);
        if (schedule == null) return false;

        if (seatClass == "Economy" && schedule.AvailableEconomySeats > 0)
            schedule.AvailableEconomySeats--;
        else if (seatClass == "Business" && schedule.AvailableBusinessSeats > 0)
            schedule.AvailableBusinessSeats--;
        else
            return false;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReleaseSeatAsync(int scheduleId, string seatClass)
    {
        var schedule = await _db.Schedules.FindAsync(scheduleId);
        if (schedule == null) return false;

        if (seatClass == "Economy")
            schedule.AvailableEconomySeats++;
        else if (seatClass == "Business")
            schedule.AvailableBusinessSeats++;

        await _db.SaveChangesAsync();
        return true;
    }
    public async Task<Schedule?> GetByIdAsync(int id)
    {
        return await _db.Schedules
            .Include(s => s.Flight)
                .ThenInclude(f => f!.OriginAirport)
            .Include(s => s.Flight)
                .ThenInclude(f => f!.DestinationAirport)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Schedule> CreateAsync(ScheduleDto dto)
    {
        var flight = await _db.Flights.FindAsync(dto.FlightId);
        if (flight == null)
            throw new Exception("Flight not found");

        var schedule = new Schedule
        {
            FlightId = dto.FlightId,
            DepartureTime = dto.DepartureTime,
            ArrivalTime = dto.ArrivalTime,
            EconomyPrice = dto.EconomyPrice,
            BusinessPrice = dto.BusinessPrice,
            AvailableEconomySeats = flight.TotalEconomySeats,
            AvailableBusinessSeats = flight.TotalBusinessSeats
        };

        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync();

        // Reload with full flight and airport data
        return await _db.Schedules
            .Include(s => s.Flight)
                .ThenInclude(f => f!.OriginAirport)
            .Include(s => s.Flight)
                .ThenInclude(f => f!.DestinationAirport)
            .FirstAsync(s => s.Id == schedule.Id);
    }

    public async Task UpdateStatusAsync(int id, string status)
    {
        var schedule = await _db.Schedules
            .Include(s => s.Flight)
                .ThenInclude(f => f!.OriginAirport)
            .Include(s => s.Flight)
                .ThenInclude(f => f!.DestinationAirport)
            .FirstOrDefaultAsync(s => s.Id == id);
            
        if (schedule == null)
            throw new Exception("Schedule not found");

        var oldStatus = schedule.Status; // Capture BEFORE overwriting
        schedule.Status = status;
        await _db.SaveChangesAsync();

        // Broadcast status change for passengers who subscribed to flight alerts
        try
        {
            await _publisher.PublishAsync("flight-status-changed", new FlightStatusChangedEvent
            {
                ScheduleId = schedule.Id,
                FlightNumber = schedule.Flight!.FlightNumber,
                Origin = schedule.Flight.OriginAirport!.Code,
                Destination = schedule.Flight.DestinationAirport!.Code,
                DepartureTime = schedule.DepartureTime,
                OldStatus = oldStatus,
                NewStatus = status
            });
        }
        catch
        {
            // Don't fail the status update if RabbitMQ is unavailable
        }
    }
}