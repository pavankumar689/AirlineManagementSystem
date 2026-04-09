using BookingService.Application.DTOs;

namespace BookingService.Application.Interfaces;

// This interface represents calling FlightService API
public interface IFlightServiceClient
{
    Task<ScheduleInfoDto?> GetScheduleAsync(int scheduleId);
    Task<bool> DeductSeatAsync(int scheduleId, string seatClass);
    Task<bool> ReleaseSeatAsync(int scheduleId, string seatClass);
}