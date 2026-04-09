using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Events;

namespace PaymentService.Infrastructure.Messaging;

public class PaymentEventConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PaymentEventConsumer> _logger;
    private readonly string _rabbitHost;
    private IConnection? _connection;
    private IChannel? _channel;

    public PaymentEventConsumer(IServiceProvider services, IConfiguration config,
        ILogger<PaymentEventConsumer> logger)
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

        await _channel!.QueueDeclareAsync("booking-created", durable: true, exclusive: false, autoDelete: false, arguments: null);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            _logger.LogDebug("Received booking-created event: {Json}", json);

            var evt = JsonSerializer.Deserialize<BookingCreatedEvent>(json);
            if (evt != null)
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

                var payment = new Payment
                {
                    BookingId = evt.BookingId,
                    PassengerId = evt.PassengerId,
                    PassengerEmail = evt.PassengerEmail,
                    PassengerName = evt.PassengerName,
                    Amount = evt.Amount,
                    Method = evt.PaymentMethod,
                    FlightNumber = evt.FlightNumber,
                    Origin = evt.Origin,
                    Destination = evt.Destination,
                    SeatNumber = evt.SeatNumber,
                    Class = evt.Class,
                    ScheduleId = evt.ScheduleId,
                    Status = "Processing"
                };

                db.Payments.Add(payment);
                await db.SaveChangesAsync();

                _logger.LogInformation("Payment record created for BookingId={BookingId}, Amount={Amount}. Awaiting Razorpay verification.",
                    evt.BookingId, evt.Amount);
            }

            await _channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await _channel.BasicConsumeAsync("booking-created", false, consumer);

        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(1000, stoppingToken);
    }
}
