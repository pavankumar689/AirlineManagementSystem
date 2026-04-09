using BookingService.Application.DTOs;

namespace BookingService.Application.Interfaces;

public interface IBookingService
{
    Task<BookingResponseDto> CreateBookingAsync(CreateBookingDto dto, int passengerId, string passengerName, string passengerEmail);
    Task<List<BookingResponseDto>> GetMyBookingsAsync(int passengerId);
    Task<BookingResponseDto> GetBookingByIdAsync(int bookingId, int passengerId);
    Task<BookingResponseDto> CancelBookingAsync(int bookingId, int passengerId);
    Task<List<BookingResponseDto>> GetAllBookingsAsync();
    Task UpdateBookingStatusAsync(int bookingId, string status, int scheduleId, string seatClass);
    Task<List<string>> GetOccupiedSeatsAsync(int scheduleId);
}