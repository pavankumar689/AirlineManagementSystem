using BookingService.Application.DTOs;
using BookingService.Application.Interfaces;
using BookingService.Domain.Entities;
using BookingService.Infrastructure.Data;
using BookingService.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Events;

namespace BookingService.Infrastructure.Services;

public class BookingServiceImpl : IBookingService
{
    private readonly BookingDbContext _db;
    private readonly IFlightServiceClient _flightClient;
    private readonly RabbitMQPublisher _publisher;
    private readonly ILogger<BookingServiceImpl> _logger;
    private readonly string _authServiceUrl;

    public BookingServiceImpl(
        BookingDbContext db,
        IFlightServiceClient flightClient,
        RabbitMQPublisher publisher,
        IConfiguration config,
        ILogger<BookingServiceImpl> logger)
    {
        _db = db;
        _flightClient = flightClient;
        _publisher = publisher;
        _logger = logger;
        _authServiceUrl = config["Services:AuthServiceUrl"] ?? "http://localhost:5228";
    }

    public async Task<BookingResponseDto> CreateBookingAsync(
        CreateBookingDto dto,
        int passengerId,
        string passengerName,
        string passengerEmail)
    {
        // SAGA STEP 1: Get schedule from FlightService via HTTP
        var schedule = await _flightClient.GetScheduleAsync(dto.ScheduleId);
        if (schedule == null)
            throw new Exception("Flight schedule not found");

        var passengerList = dto.Passengers != null && dto.Passengers.Any() 
            ? dto.Passengers 
            : new List<PassengerRequestDto> { new PassengerRequestDto { Name = passengerName, Email = passengerEmail } };

        // Check seat availability
        if (dto.Class == "Economy" && schedule.AvailableEconomySeats < passengerList.Count)
            throw new Exception("Not enough economy seats available");
        if (dto.Class == "Business" && schedule.AvailableBusinessSeats < passengerList.Count)
            throw new Exception("Not enough business seats available");

        var unitPrice = dto.Class == "Economy"
            ? schedule.EconomyPrice
            : schedule.BusinessPrice;

        var occupiedSeats = await GetOccupiedSeatsAsync(dto.ScheduleId);

        var pnr = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        var allBookings = new List<Booking>();
        var massAmount = 0m;

        foreach (var p in passengerList)
        {
            var seatNumber = string.IsNullOrWhiteSpace(p.SeatNumber) ? GenerateSeatNumber(dto.Class) : p.SeatNumber.Trim().ToUpper();

            if (occupiedSeats.Contains(seatNumber))
                throw new Exception($"Seat {seatNumber} is already taken");

            var seatPrice = unitPrice;
            if (seatNumber.EndsWith("A") || seatNumber.EndsWith("F"))
            {
                seatPrice = unitPrice * 1.15m;
            }
            massAmount += seatPrice;

            var b = new Booking
            {
                PassengerId = passengerId, // Logged in user owns all bookings
                PassengerName = string.IsNullOrWhiteSpace(p.Name) ? passengerName : p.Name,
                PassengerEmail = string.IsNullOrWhiteSpace(p.Email) ? passengerEmail : p.Email,
                ScheduleId = dto.ScheduleId,
                FlightNumber = schedule.FlightNumber,
                Origin = schedule.OriginCode,
                Destination = schedule.DestinationCode,
                DepartureTime = schedule.DepartureTime,
                Class = dto.Class,
                SeatNumber = seatNumber,
                TotalAmount = seatPrice,
                Status = "Pending",
                PNR = pnr
            };
            allBookings.Add(b);
            _db.Bookings.Add(b);
            occupiedSeats.Add(seatNumber); // prevent duplicate booking in the same request loop
        }

        _logger.LogInformation("Creating booking for PassengerId={PassengerId}, ScheduleId={ScheduleId}, Class={Class}, Passengers={Count}",
            passengerId, dto.ScheduleId, dto.Class, passengerList.Count);

        await _db.SaveChangesAsync();

        var primaryBooking = allBookings.First();
        _logger.LogInformation("Booking created: PNR={PNR}, BookingId={BookingId}, TotalAmount={Amount}",
            primaryBooking.PNR, primaryBooking.Id, massAmount);

        // Publish event for Payment processing using primary booking id
        await _publisher.PublishAsync("booking-created", new BookingCreatedEvent
        {
            BookingId = primaryBooking.Id,
            PassengerId = passengerId,
            PassengerName = primaryBooking.PassengerName,
            PassengerEmail = primaryBooking.PassengerEmail,
            FlightNumber = schedule.FlightNumber,
            Origin = schedule.OriginCode,
            Destination = schedule.DestinationCode,
            DepartureTime = schedule.DepartureTime,
            Class = dto.Class,
            SeatNumber = primaryBooking.SeatNumber,
            Amount = massAmount, // Ask PaymentService to bill the TOTAL amount for all passengers on this unified Order!
            PaymentMethod = dto.PaymentMethod,
            ScheduleId = dto.ScheduleId
        });

        // Return a representation of the massive master booking for the frontend
        var response = MapToResponse(primaryBooking);
        response.TotalAmount = massAmount; 

        return response;
    }

    public async Task UpdateBookingStatusAsync(
        int bookingId,
        string status,
        int scheduleId,
        string seatClass)
    {
        var primaryBooking = await _db.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (primaryBooking == null) return;

        // If part of a mass booking, find all linked by PNR
        var groupBookings = await _db.Bookings
            .Where(b => b.PNR == primaryBooking.PNR)
            .ToListAsync();

        foreach (var b in groupBookings)
        {
            if (b.Status == status) continue; // Prevent double-execution from Race conditions (HTTP + RabbitMQ both hitting)

            b.Status = status;

            if (status == "Confirmed")
            {
                // Deduct seat from FlightService for EACH passenger
                await _flightClient.DeductSeatAsync(scheduleId, seatClass);
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<BookingResponseDto>> GetMyBookingsAsync(int passengerId)
    {
        var bookings = await _db.Bookings
            .Where(b => b.PassengerId == passengerId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return bookings.Select(b => MapToResponse(b)).ToList();
    }

    public async Task<BookingResponseDto> GetBookingByIdAsync(int bookingId, int passengerId)
    {
        var booking = await _db.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.PassengerId == passengerId);

        if (booking == null)
            throw new Exception("Booking not found");

        return MapToResponse(booking);
    }

    public async Task<BookingResponseDto> CancelBookingAsync(int bookingId, int passengerId)
    {
        var booking = await _db.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.PassengerId == passengerId);

        if (booking == null)
            throw new Exception("Booking not found");

        if (booking.Status != "Confirmed")
            throw new Exception("Only confirmed bookings can be cancelled");

        booking.Status = "Cancelled";

        // Release seat back
        await _flightClient.ReleaseSeatAsync(booking.ScheduleId, booking.Class);

        await _db.SaveChangesAsync();

        // Refund reward points that were redeemed for this booking
        try
        {
            using var http = new HttpClient();
            await http.PostAsync(
                $"{_authServiceUrl}/api/auth/rewards/refund?userId={booking.PassengerId}&referenceId={booking.Id}",
                null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refund reward points for PassengerId={PassengerId}, BookingId={BookingId}",
                booking.PassengerId, booking.Id);
        }

        // Publish cancellation event
        await _publisher.PublishAsync("booking-cancelled", new BookingCancelledEvent
        {
            BookingId = booking.Id,
            PassengerEmail = booking.PassengerEmail,
            PassengerName = booking.PassengerName,
            FlightNumber = booking.FlightNumber,
            RefundAmount = booking.TotalAmount * 0.90m
        });

        return MapToResponse(booking);
    }

    public async Task<List<string>> GetOccupiedSeatsAsync(int scheduleId)
    {
        // A seat is "occupied" only if:
        //  - Status is Confirmed, OR
        //  - Status is Pending AND the booking was created within the last 15 minutes
        //    (anything older = payment never completed, treat seat as free again)
        var cutoff = DateTime.UtcNow.AddMinutes(-15);

        return await _db.Bookings
            .Where(b => b.ScheduleId == scheduleId
                && b.Status != "Cancelled"
                && !(b.Status == "Pending" && b.CreatedAt < cutoff))
            .Select(b => b.SeatNumber)
            .ToListAsync();
    }

    public async Task<List<BookingResponseDto>> GetAllBookingsAsync()
    {
        var bookings = await _db.Bookings
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return bookings.Select(b => MapToResponse(b)).ToList();
    }

    private string GenerateSeatNumber(string seatClass)
    {
        var random = new Random();
        var row = random.Next(1, 30);
        var seat = (char)('A' + random.Next(0, 6));
        var prefix = seatClass == "Business" ? "B" : "E";
        return $"{prefix}{row}{seat}";
    }

    private BookingResponseDto MapToResponse(Booking booking)
    {
        return new BookingResponseDto
        {
            BookingId = booking.Id,
            PNR = booking.PNR,
            PassengerName = booking.PassengerName,
            PassengerEmail = booking.PassengerEmail,
            FlightNumber = booking.FlightNumber,
            Origin = booking.Origin,
            Destination = booking.Destination,
            DepartureTime = booking.DepartureTime,
            Class = booking.Class,
            SeatNumber = booking.SeatNumber,
            TotalAmount = booking.TotalAmount,
            BookingStatus = booking.Status,
            PaymentStatus = booking.Status == "Confirmed" ? "Success"
                          : booking.Status == "Cancelled" ? "Refunded"
                          : "Processing",
            PaymentMethod = "Online",
            CreatedAt = booking.CreatedAt
        };
    }
}

