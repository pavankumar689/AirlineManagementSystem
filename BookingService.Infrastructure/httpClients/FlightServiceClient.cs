using System.Net.Http.Json;
using BookingService.Application.DTOs;
using BookingService.Application.Interfaces;

namespace BookingService.Infrastructure.HttpClients;

public class FlightServiceClient : IFlightServiceClient
{
    private readonly HttpClient _httpClient;

    public FlightServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ScheduleInfoDto?> GetScheduleAsync(int scheduleId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/schedule/{scheduleId}");
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ScheduleInfoDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeductSeatAsync(int scheduleId, string seatClass)
    {
        try
        {
            var response = await _httpClient.PatchAsync(
                $"/api/schedule/{scheduleId}/deduct-seat?seatClass={seatClass}",
                null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ReleaseSeatAsync(int scheduleId, string seatClass)
    {
        try
        {
            var response = await _httpClient.PatchAsync(
                $"/api/schedule/{scheduleId}/release-seat?seatClass={seatClass}",
                null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}