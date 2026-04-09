using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Services;
using PaymentService.Application.DTOs;
using PaymentService.Infrastructure.Data;
using PaymentService.Infrastructure.Messaging;
using Shared.Events;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly RazorpayService _razorpayService;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentController> _logger;
    private readonly string _bookingServiceUrl;
    private readonly string _authServiceUrl;

    public PaymentController(
        IPaymentService paymentService,
        RazorpayService razorpayService,
        IConfiguration config,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _razorpayService = razorpayService;
        _config = config;
        _logger = logger;
        _bookingServiceUrl = config["Services:BookingServiceUrl"] ?? "http://localhost:5122";
        _authServiceUrl    = config["Services:AuthServiceUrl"]    ?? "http://localhost:5228";
    }

    /// <summary>
    /// Internal / Admin endpoint to view all recorded payments.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var payments = await _paymentService.GetAllPaymentsAsync();
        return Ok(payments);
    }

    /// <summary>
    /// Passenger endpoint to view their own payment history.
    /// </summary>
    [HttpGet("my-payments")]
    public async Task<IActionResult> GetMyPayments()
    {
        var passengerId = int.Parse(Request.Headers["X-User-Id"].ToString());
        var payments = await _paymentService.GetPaymentsByPassengerAsync(passengerId);
        return Ok(payments);
    }

    /// <summary>
    /// Initiates a new Razorpay payment order.
    /// Communicates with Razorpay's API to construct an order required before frontend checkout.
    /// </summary>
    [HttpPost("create-order")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        try
        {
            var orderId = _razorpayService.CreateOrder(dto.Amount);
            return Ok(new
            {
                orderId,
                amount = dto.Amount,
                currency = "INR",
                keyId = _config["Razorpay:KeyId"]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateOrder for Amount={Amount}", dto.Amount);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// The critical Saga Orchestrator step. Called strictly when the passenger completes Razorpay payment on the frontend.
    /// 1. Verifies Razorpay cryptographic signature.
    /// 2. Updates local DB payment status.
    /// 3. Communicates synchronously with BookingService to Confirm the Booking.
    /// 4. Communicates synchronously with AuthService to award Reward Points.
    /// 5. Publishes async domain events (RabbitMQ) to trigger email/notification systems.
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment(
        [FromBody] VerifyPaymentDto dto,
        [FromServices] PaymentDbContext _db,
        [FromServices] RabbitMQPublisher _publisher)
    {
        var isValid = _razorpayService.VerifyPayment(
            dto.OrderId,
            dto.PaymentId,
            dto.Signature);

        if (isValid)
        {
            PaymentService.Domain.Entities.Payment payment = null;
            for (int i = 0; i < 10; i++)
            {
                payment = _db.Payments.FirstOrDefault(p => p.BookingId == dto.BookingId);
                if (payment != null) break;
                await Task.Delay(500);
            }

            if (payment != null)
            {
                payment.Status = "Success";
                payment.PaidAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                try
                {
                    using var http = new HttpClient();
                    var url = $"{_bookingServiceUrl}/api/booking/{payment.BookingId}/confirm?scheduleId={payment.ScheduleId}&seatClass={payment.Class}";
                    await http.PostAsync(url, null);
                    _logger.LogInformation("Booking {BookingId} confirmed via HTTP.", payment.BookingId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to confirm booking {BookingId} synchronously.", payment.BookingId);
                }

                try
                {
                    using var http2 = new HttpClient();
                    var earnBody = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        userId = payment.PassengerId,
                        amountPaid = payment.Amount,
                        referenceId = payment.BookingId.ToString()
                    });
                    await http2.PostAsync(
                        $"{_authServiceUrl}/api/auth/rewards/earn",
                        new StringContent(earnBody, System.Text.Encoding.UTF8, "application/json"));
                    _logger.LogInformation("Reward points earn request sent for PassengerId={PassengerId}, Amount={Amount}.",
                        payment.PassengerId, payment.Amount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to award reward points for PassengerId={PassengerId}.", payment.PassengerId);
                }

                try
                {
                    await _publisher.PublishAsync("payment-completed", new PaymentCompletedEvent
                    {
                        BookingId = payment.BookingId,
                        PassengerId = payment.PassengerId,
                        PassengerEmail = payment.PassengerEmail,
                        PassengerName = payment.PassengerName,
                        FlightNumber = payment.FlightNumber,
                        Origin = payment.Origin,
                        Destination = payment.Destination,
                        DepartureTime = DateTime.UtcNow,
                        SeatNumber = payment.SeatNumber,
                        Amount = payment.Amount,
                        ScheduleId = payment.ScheduleId,
                        Class = payment.Class
                    });
                }
                catch
                {
                    // Ignore RabbitMQ errors if not running
                }
            }

            return Ok(new { message = "Payment verified", paymentId = dto.PaymentId });
        }

        return BadRequest(new { message = "Payment verification failed" });
    }
}
