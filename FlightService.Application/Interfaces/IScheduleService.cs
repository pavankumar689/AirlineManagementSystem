using FlightService.Application.DTOs;
using FlightService.Domain.Entities;

namespace FlightService.Application.Interfaces;

public interface IScheduleService
{
    Task<List<Schedule>> SearchAsync(FlightSearchDto dto);
    Task<List<Schedule>> GetAllAsync();
    Task<Schedule?> GetByIdAsync(int id);
    Task<Schedule> CreateAsync(ScheduleDto dto);
    Task UpdateStatusAsync(int id, string status);
    Task<bool> DeductSeatAsync(int scheduleId, string seatClass);
    Task<bool> ReleaseSeatAsync(int scheduleId, string seatClass);
}