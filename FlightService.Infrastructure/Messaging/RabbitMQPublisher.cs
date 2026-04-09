using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace FlightService.Infrastructure.Messaging;

public class RabbitMQPublisher
{
    private readonly ConnectionFactory _factory;

    public RabbitMQPublisher(IConfiguration config)
    {
        var host = config["RabbitMQ:Host"] ?? "localhost";
        _factory = new ConnectionFactory
        {
            HostName = host,
            UserName = "guest",
            Password = "guest"
        };
    }

    public async Task PublishAsync<T>(string queueName, T message)
    {
        using var connection = await _factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties(),
            body: body);

        Console.WriteLine($"[x] Sent strictly to {queueName} - Message: {json}");
    }
}
