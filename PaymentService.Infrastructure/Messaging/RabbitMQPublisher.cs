using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace PaymentService.Infrastructure.Messaging;

public class RabbitMQPublisher : IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    public RabbitMQPublisher(IConfiguration config)
    {
        var host = config["RabbitMQ:Host"] ?? "localhost";
        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = "guest",
            Password = "guest"
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
    }

    public async Task PublishAsync<T>(string queueName, T message)
    {
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body);

        Console.WriteLine($"Published to {queueName}: {json}");
    }

    public void Dispose()
    {
        _channel?.CloseAsync();
        _connection?.CloseAsync();
    }
}