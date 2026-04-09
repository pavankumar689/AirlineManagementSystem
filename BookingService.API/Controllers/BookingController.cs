using BookingService.Application.DTOs;
using BookingService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// Creates a new flight booking.
    /// This endpoint is protected by the API Gateway to ensure only authenticated users
    /// with the 'Passenger' role can access it. Expected HTTP headers (X-User-Id, etc.) are injected by Ocelot Gateway.
    /// </summary>
    /// <param name="dto">The data transfer object containing the booking details.</param>
    /// <returns>The newly created booking with PNR if successful.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateBooking(CreateBookingDto dto)
    {
        try
        {
            var passengerId = int.Parse(Request.Headers["X-User-Id"].ToString());
            var passengerName = Request.Headers["X-User-Name"].ToString();
            var passengerEmail = Request.Headers["X-User-Email"].ToString();

            var booking = await _bookingService.CreateBookingAsync(
                dto, passengerId, passengerName, passengerEmail);

            return Ok(booking);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }



    /// <summary>
    /// Retrieves a list of occupied seat numbers for the specified schedule.
    /// </summary>
    /// <param name="scheduleId">The unique identifier of the schedule for which to retrieve occupied seats.</param>
    /// <returns>An IActionResult containing a collection of occupied seat numbers for the given schedule. Returns a BadRequest
    /// result if an error occurs.</returns>

    // Public — no auth needed
    [HttpGet("occupied-seats/{scheduleId}")]
    public async Task<IActionResult> GetOccupiedSeats(int scheduleId)
    {
        try
        {
            var occupiedSeats = await _bookingService.GetOccupiedSeatsAsync(scheduleId);
            return Ok(occupiedSeats);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Passenger views their bookings — gateway enforces Passenger role


    /// <summary>
    /// Retrieves the list of bookings associated with the currently authenticated passenger.
    /// </summary>
    /// <remarks>This endpoint is accessible only to users with the Passenger role. The passenger is
    /// identified by the 'X-User-Id' header in the request. The response includes all bookings for the authenticated
    /// passenger.</remarks>
    /// <returns>An <see cref="IActionResult"/> containing the list of bookings for the passenger if successful; otherwise, a bad
    /// request result with an error message.</returns>
    [HttpGet("my-bookings")]
    public async Task<IActionResult> GetMyBookings()
    {
        try
        {
            var passengerId = int.Parse(Request.Headers["X-User-Id"].ToString());
            var bookings = await _bookingService.GetMyBookingsAsync(passengerId);
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves a specific booking detail for the currently authenticated passenger by its Booking ID.
    /// Access is restricted to the specific passenger owning the booking to prevent unauthorized data access.
    /// </summary>
    /// <param name="id">The unique identifier of the booking.</param>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(int id)
    {
        try
        {
            var passengerId = int.Parse(Request.Headers["X-User-Id"].ToString());
            var booking = await _bookingService.GetBookingByIdAsync(id, passengerId);
            return Ok(booking);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancels a booking via the Passenger Portal. 
    /// This changes the status of the booking to "Cancelled" and frees up the reserved seats.
    /// </summary>
    /// <param name="id">The ID of the booking to cancel.</param>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        try
        {
            var passengerId = int.Parse(Request.Headers["X-User-Id"].ToString());
            var booking = await _bookingService.CancelBookingAsync(id, passengerId);
            return Ok(booking);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Administrator endpoint to view all bookings in the entire system.
    /// API Gateway route configuration ensures only Admin/SuperAdmin accounts can hit this route.
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllBookings()
    {
        try
        {
            var bookings = await _bookingService.GetAllBookingsAsync();
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Internal endpoint used for the Saga Orchestration pattern.
    /// It is called exclusively via a server-to-server request from the PaymentService to officially confirm a booking
    /// once the Razorpay payment succeeds.
    /// </summary>
    /// <param name="id">The booking ID.</param>
    /// <param name="scheduleId">The ID of the flight schedule to update seat counts.</param>
    /// <param name="seatClass">Economy/Business/First to appropriately decrease available seats.</param>
    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> ConfirmBooking(int id, [FromQuery] int scheduleId, [FromQuery] string seatClass)
    {
        try
        {
            await _bookingService.UpdateBookingStatusAsync(id, "Confirmed", scheduleId, seatClass ?? "Economy");
            return Ok(new { message = "Booking confirmed" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
