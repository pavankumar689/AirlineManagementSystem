using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Infrastructure.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Events;

namespace NotificationService.Infrastructure.Messaging;

public class NotificationEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationEventConsumer> _logger;
    private readonly string _rabbitHost;
    private IConnection? _connection;
    private IChannel? _channel;

    public NotificationEventConsumer(IServiceProvider services, IConfiguration config,
        ILogger<NotificationEventConsumer> logger)
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

        await _channel!.QueueDeclareAsync("booking-confirmed", durable: true, exclusive: false, autoDelete: false, arguments: null);
        await _channel.QueueDeclareAsync("booking-cancelled", durable: true, exclusive: false, autoDelete: false, arguments: null);
        await _channel.QueueDeclareAsync("flight-status-changed", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var confirmedConsumer = new AsyncEventingBasicConsumer(_channel);
        confirmedConsumer.ReceivedAsync += async (model, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<BookingConfirmedEvent>(json, options);
            if (evt != null)
            {
                _logger.LogInformation("Sending booking-confirmed email to {Email} for BookingId={BookingId} PNR={PNR}",
                    evt.PassengerEmail, evt.BookingId, evt.PNR);
                using var scope = _services.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                await emailService.SendBookingConfirmedAsync(
                    evt.PassengerEmail, evt.PassengerName, evt.FlightNumber,
                    evt.Origin, evt.Destination, evt.DepartureTime,
                    evt.SeatNumber, evt.Class, evt.Amount, evt.PNR);
            }
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        var cancelledConsumer = new AsyncEventingBasicConsumer(_channel);
        cancelledConsumer.ReceivedAsync += async (model, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<BookingCancelledEvent>(json, options);
            if (evt != null)
            {
                _logger.LogInformation("Sending booking-cancelled email to {Email} for BookingId={BookingId}",
                    evt.PassengerEmail, evt.BookingId);
                using var scope = _services.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                await emailService.SendBookingCancelledAsync(
                    evt.PassengerEmail, evt.PassengerName, evt.FlightNumber, evt.RefundAmount);
            }
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        var alertConsumer = new AsyncEventingBasicConsumer(_channel);
        alertConsumer.ReceivedAsync += async (model, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<FlightStatusChangedEvent>(json, options);
            if (evt != null)
            {
                _logger.LogInformation("FlightStatusChanged: Flight={Flight} {Old}→{New}, ScheduleId={ScheduleId}",
                    evt.FlightNumber, evt.OldStatus, evt.NewStatus, evt.ScheduleId);
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationService.Infrastructure.Data.NotificationDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                var subscribers = db.FlightAlerts
                    .Where(a => a.ScheduleId == evt.ScheduleId && a.IsActive)
                    .ToList();

                _logger.LogInformation("Sending flight alert to {Count} subscriber(s) for ScheduleId={ScheduleId}",
                    subscribers.Count, evt.ScheduleId);

                foreach (var sub in subscribers)
                {
                    await emailService.SendFlightAlertAsync(
                        sub.PassengerEmail, sub.PassengerName, evt.FlightNumber,
                        evt.Origin, evt.Destination, evt.DepartureTime,
                        evt.OldStatus, evt.NewStatus);
                }
            }
            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await _channel.BasicConsumeAsync("booking-confirmed", false, confirmedConsumer);
        await _channel.BasicConsumeAsync("booking-cancelled", false, cancelledConsumer);
        await _channel.BasicConsumeAsync("flight-status-changed", false, alertConsumer);

        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(1000, stoppingToken);
    }
}
