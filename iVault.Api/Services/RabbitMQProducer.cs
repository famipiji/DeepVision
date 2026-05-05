using iVault.Api.Events;    
using iVault.Api.Interfaces; 
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace iVault.Api.Services;

public class RabbitMQProducer : IMessageProducer
{
    private readonly IConnection _connection;
    private const string ExchangeName = "ivault.records.exchange";

    public RabbitMQProducer(IConnection connection)
    {
        _connection = connection;
    }

    public async Task PublishRecordIngestedAsync(RecordIngestedEvent @event)
    {
        using var channel = await _connection.CreateChannelAsync();

        const string QueueName = "ivault.ocr.queue"; // Must match Worker!

        // 1. Declare the exchange
        await channel.ExchangeDeclareAsync(exchange: ExchangeName, type: ExchangeType.Topic, durable: true);

        // 2. Ensure the queue exists
        await channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false);

        // 3. Bind the queue to the exchange using the routing key
        await channel.QueueBindAsync(queue: QueueName, exchange: ExchangeName, routingKey: "record.ingested");

        var json = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(json);

        // 4. Publish
        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: "record.ingested",
            body: body);
    }
}