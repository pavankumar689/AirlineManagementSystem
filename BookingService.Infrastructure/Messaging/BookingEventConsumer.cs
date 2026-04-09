using System.Text;
using System.Text.Json;
using BookingService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Events;

namespace BookingService.Infrastructure.Messaging;

public class BookingEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BookingEventConsumer> _logger;
    private readonly string _rabbitHost;
    private IConnection? _connection;
    private IChannel? _channel;

    public BookingEventConsumer(IServiceProvider services, IConfiguration config,
        ILogger<BookingEventConsumer> logger)
    {
        _services = services;
        _logger = logger;
        _rabbitHost = config["RabbitMQ:Host"] ?? "localhost";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitHost,
            UserName = "guest",
            Password = "guest"
        };

        const int maxRetries = 10;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync();
                _channel    = await _connection.CreateChannelAsync();
                _logger.LogInformation("Connected to RabbitMQ successfully.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ connection attempt {Attempt}/{Max} failed: {Error}. Retrying in 5s...",
                    attempt, maxRetries, ex.Message);
                if (attempt == maxRetries)
                {
                    _logger.LogError("All retries exhausted — running without event consumer.");
                    while (!stoppingToken.IsCancellationRequested)
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        await _channel!.QueueDeclareAsync("payment-completed", durable: true, exclusive: false, autoDelete: false, arguments: null);
        await _channel.QueueDeclareAsync("payment-failed", durable: true, exclusive: false, autoDelete: false, arguments: null);

        // Consumer for PaymentCompleted
        var completedConsumer = new AsyncEventingBasicConsumer(_channel);
        completedConsumer.ReceivedAsync += async (model, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<PaymentCompletedEvent>(json);

            if (evt != null)
            {
                _logger.LogInformation("PaymentCompleted received for BookingId={BookingId}", evt.BookingId);
                using var scope = _services.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                await bookingService.UpdateBookingStatusAsync(evt.BookingId, "Confirmed", evt.ScheduleId, evt.Class);
                _logger.LogInformation("Booking {BookingId} confirmed via payment-completed event.", evt.BookingId);

                var db = scope.ServiceProvider.GetRequiredService<BookingService.Infrastructure.Data.BookingDbContext>();
                var booking = await db.Bookings.FindAsync(evt.BookingId);
                var pnr = booking?.PNR ?? "";

                var publisher = scope.ServiceProvider.GetRequiredService<RabbitMQPublisher>();
                await publisher.PublishAsync("booking-confirmed", new BookingConfirmedEvent
                {
                    BookingId = evt.BookingId,
                    PNR = pnr,
                    PassengerEmail = evt.PassengerEmail,
                    PassengerName = evt.PassengerName,
                    FlightNumber = evt.FlightNumber,
                    Origin = evt.Origin,
                    Destination = evt.Destination,
                    DepartureTime = evt.DepartureTime,
                    SeatNumber = evt.SeatNumber,
                    Amount = evt.Amount,
                    Class = evt.Class
                });
            }

            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        // Consumer for PaymentFailed
        var failedConsumer = new AsyncEventingBasicConsumer(_channel);
        failedConsumer.ReceivedAsync += async (model, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<PaymentFailedEvent>(json);

            if (evt != null)
            {
                _logger.LogWarning("PaymentFailed received for BookingId={BookingId}. Reason: {Reason}",
                    evt.BookingId, evt.Reason);
                using var scope = _services.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                await bookingService.UpdateBookingStatusAsync(evt.BookingId, "Cancelled", 0, "");
                _logger.LogInformation("Booking {BookingId} cancelled due to payment failure.", evt.BookingId);

                var publisher = scope.ServiceProvider.GetRequiredService<RabbitMQPublisher>();
                await publisher.PublishAsync("booking-cancelled", new BookingCancelledEvent
                {
                    BookingId = evt.BookingId,
                    PassengerEmail = evt.PassengerEmail,
                    PassengerName = evt.PassengerName,
                    FlightNumber = evt.FlightNumber,
                    RefundAmount = 0
                });
            }

            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await _channel.BasicConsumeAsync("payment-completed", false, completedConsumer);
        await _channel.BasicConsumeAsync("payment-failed", false, failedConsumer);

        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(1000, stoppingToken);
    }
}
