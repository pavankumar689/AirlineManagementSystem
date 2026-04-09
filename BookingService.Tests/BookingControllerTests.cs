using BookingService.API.Controllers;
using BookingService.Application.DTOs;
using BookingService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace BookingService.Tests;

/// <summary>
/// Unit tests for BookingController.
/// All dependencies are mocked — no database or network required.
/// User identity is injected via X-User-* headers (gateway-forwarded pattern).
/// </summary>
[TestFixture]
public class BookingControllerTests
{
    private Mock<IBookingService> _mockService = null!;
    private BookingController _controller = null!;

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Sets X-User-* headers on the controller's HttpContext.</summary>
    private void SetUserHeaders(int userId = 1, string name = "Test User", string email = "test@test.com")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"]    = userId.ToString();
        httpContext.Request.Headers["X-User-Name"]  = name;
        httpContext.Request.Headers["X-User-Email"] = email;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static BookingResponseDto MakeBookingResponse(int id = 1, string status = "Pending") =>
        new BookingResponseDto
        {
            BookingId     = id,
            PNR           = "ABC123",
            PassengerName = "Test User",
            PassengerEmail= "test@test.com",
            FlightNumber  = "VK-100",
            Origin        = "DEL",
            Destination   = "DXB",
            DepartureTime = DateTime.UtcNow.AddDays(1),
            Class         = "Economy",
            SeatNumber    = "E12A",
            TotalAmount   = 15000m,
            BookingStatus = status,
            PaymentStatus = "Processing",
            PaymentMethod = "Online",
            CreatedAt     = DateTime.UtcNow
        };

    // ── Setup / Teardown ─────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _mockService = new Mock<IBookingService>();
        _controller  = new BookingController(_mockService.Object);
        SetUserHeaders(); // default user for most tests
    }

    // ── CreateBooking ─────────────────────────────────────────────────────────

    [Test]
    public async Task CreateBooking_ValidRequest_Returns200WithBookingResponse()
    {
        // Arrange
        var dto = new CreateBookingDto { ScheduleId = 1, Class = "Economy", PaymentMethod = "UPI" };
        var expected = MakeBookingResponse();

        _mockService
            .Setup(s => s.CreateBookingAsync(dto, 1, "Test User", "test@test.com"))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.CreateBooking(dto) as OkObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));
        Assert.That(result.Value, Is.EqualTo(expected));
    }

    [Test]
    public async Task CreateBooking_ServiceThrows_Returns400WithMessage()
    {
        // Arrange
        var dto = new CreateBookingDto { ScheduleId = 99, Class = "Economy" };

        _mockService
            .Setup(s => s.CreateBookingAsync(It.IsAny<CreateBookingDto>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Flight schedule not found"));

        // Act
        var result = await _controller.CreateBooking(dto) as BadRequestObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task CreateBooking_MissingUserIdHeader_Returns400()
    {
        // Arrange — no headers set, X-User-Id will be empty string → int.Parse("") → 0
        // The service will be called with passengerId=0 and throw or return an error
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        var dto = new CreateBookingDto { ScheduleId = 1, Class = "Economy" };

        _mockService
            .Setup(s => s.CreateBookingAsync(It.IsAny<CreateBookingDto>(), 0,
                It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Invalid passenger"));

        // Act
        var result = await _controller.CreateBooking(dto) as BadRequestObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(400));
    }

    // ── GetOccupiedSeats ──────────────────────────────────────────────────────

    [Test]
    public async Task GetOccupiedSeats_ValidScheduleId_Returns200WithSeatList()
    {
        // Arrange
        var seats = new List<string> { "E1A", "E2B", "B1A" };
        _mockService.Setup(s => s.GetOccupiedSeatsAsync(5)).ReturnsAsync(seats);

        // Act
        var result = await _controller.GetOccupiedSeats(5) as OkObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));
        Assert.That(result.Value, Is.EqualTo(seats));
    }

    [Test]
    public async Task GetOccupiedSeats_NoBookings_ReturnsEmptyList()
    {
        // Arrange
        _mockService.Setup(s => s.GetOccupiedSeatsAsync(99)).ReturnsAsync(new List<string>());

        // Act
        var result = await _controller.GetOccupiedSeats(99) as OkObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        var list = result!.Value as List<string>;
        Assert.That(list, Is.Empty);
    }

    // ── GetMyBookings ─────────────────────────────────────────────────────────

    [Test]
    public async Task GetMyBookings_ReturnsPassengerBookings()
    {
        // Arrange
        var bookings = new List<BookingResponseDto>
        {
            MakeBookingResponse(1, "Confirmed"),
            MakeBookingResponse(2, "Pending")
        };
        _mockService.Setup(s => s.GetMyBookingsAsync(1)).ReturnsAsync(bookings);

        // Act
        var result = await _controller.GetMyBookings() as OkObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));
        var returned = result.Value as List<BookingResponseDto>;
        Assert.That(returned!.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GetMyBookings_ServiceThrows_Returns400()
    {
        // Arrange
        _mockService.Setup(s => s.GetMyBookingsAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _controller.GetMyBookings() as BadRequestObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(400));
    }

    // ── GetBooking ────────────────────────────────────────────────────────────

    [Test]
    public async Task GetBooking_ExistingBooking_Returns200()
    {
        // Arrange
        var booking = MakeBookingResponse(42, "Confirmed");
        _mockService.Setup(s => s.GetBookingByIdAsync(42, 1)).ReturnsAsync(booking);

        // Act
        var result = await _controller.GetBooking(42) as OkObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));
        var returned = result.Value as BookingResponseDto;
        Assert.That(returned!.BookingId, Is.EqualTo(42));
    }

    [Test]
    public async Task GetBooking_NotFound_Returns400WithMessage()
    {
        // Arrange
        _mockService.Setup(s => s.GetBookingByIdAsync(999, 1))
            .ThrowsAsync(new Exception("Booking not found"));

        // Act
        var result = await _controller.GetBooking(999) as BadRequestObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(400));
    }

    // ── CancelBooking ─────────────────────────────────────────────────────────

    [Test]
    public async Task CancelBooking_ConfirmedBooking_Returns200WithCancelledStatus()
    {
        // Arrange
        var cancelled = MakeBookingResponse(10, "Cancelled");
        _mockService.Setup(s => s.CancelBookingAsync(10, 1)).ReturnsAsync(cancelled);

        // Act
        var result = await _controller.CancelBooking(10) as OkObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));
        var returned = result.Value as BookingResponseDto;
        Assert.That(returned!.BookingStatus, Is.EqualTo("Cancelled"));
    }

    [Test]
    public async Task CancelBooking_PendingBooking_Returns400()
    {
        // Arrange — only Confirmed bookings can be cancelled
        _mockService.Setup(s => s.CancelBookingAsync(5, 1))
            .ThrowsAsync(new Exception("Only confirmed bookings can be cancelled"));

        // Act
        var result = await _controller.CancelBooking(5) as BadRequestObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(400));
    }

    // ── GetAllBookings ────────────────────────────────────────────────────────

    [Test]
    public async Task GetAllBookings_ReturnsAllBookings()
    {
        // Arrange
        var all = new List<BookingResponseDto>
        {
            MakeBookingResponse(1, "Confirmed"),
            MakeBookingResponse(2, "Cancelled"),
            MakeBookingResponse(3, "Pending")
        };
        _mockService.Setup(s => s.GetAllBookingsAsync()).ReturnsAsync(all);

        // Act
        var result = await _controller.GetAllBookings() as OkObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        var returned = result!.Value as List<BookingResponseDto>;
        Assert.That(returned!.Count, Is.EqualTo(3));
    }

    // ── ConfirmBooking ────────────────────────────────────────────────────────

    [Test]
    public async Task ConfirmBooking_ValidCall_Returns200WithMessage()
    {
        // Arrange — internal server-to-server call from PaymentService
        _mockService
            .Setup(s => s.UpdateBookingStatusAsync(7, "Confirmed", 3, "Economy"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ConfirmBooking(7, 3, "Economy") as OkObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));
        _mockService.Verify(s => s.UpdateBookingStatusAsync(7, "Confirmed", 3, "Economy"), Times.Once);
    }

    [Test]
    public async Task ConfirmBooking_ServiceThrows_Returns400()
    {
        // Arrange
        _mockService
            .Setup(s => s.UpdateBookingStatusAsync(It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Booking not found"));

        // Act
        var result = await _controller.ConfirmBooking(999, 1, "Economy") as BadRequestObjectResult;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(400));
    }

    // ── Service call verification ─────────────────────────────────────────────

    [Test]
    public async Task CreateBooking_CallsServiceWithCorrectPassengerInfo()
    {
        // Arrange
        SetUserHeaders(userId: 42, name: "John Doe", email: "john@example.com");
        var dto = new CreateBookingDto { ScheduleId = 5, Class = "Business" };
        _mockService
            .Setup(s => s.CreateBookingAsync(dto, 42, "John Doe", "john@example.com"))
            .ReturnsAsync(MakeBookingResponse());

        // Act
        await _controller.CreateBooking(dto);

        // Assert — verify the service was called with the exact header values
        _mockService.Verify(
            s => s.CreateBookingAsync(dto, 42, "John Doe", "john@example.com"),
            Times.Once);
    }

    [Test]
    public async Task GetMyBookings_CallsServiceWithCorrectPassengerId()
    {
        // Arrange
        SetUserHeaders(userId: 99);
        _mockService.Setup(s => s.GetMyBookingsAsync(99)).ReturnsAsync(new List<BookingResponseDto>());

        // Act
        await _controller.GetMyBookings();

        // Assert
        _mockService.Verify(s => s.GetMyBookingsAsync(99), Times.Once);
    }
}
