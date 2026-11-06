using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ExecutionService.Application.Interfaces;
using ExecutionService.Domain.Events;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ExecutionService.Infrastructure.Messaging;

/// <summary>
/// Publica eventos no exchange execution-service-events.
/// </summary>
public class RabbitMqEventPublisher(IConnection connection, ILogger<RabbitMqEventPublisher>? logger = null)
    : IEventPublisher, IAsyncDisposable, IDisposable
{
    private IChannel? _channel;

    private async Task<IChannel> GetChannelAsync()
    {
        if (_channel is null || !_channel.IsOpen)
            _channel = await connection.CreateChannelAsync();
        return _channel;
    }

    public async Task PublishAsync(DomainEvent @event, CancellationToken ct = default)
    {
        var channel = await GetChannelAsync();
        const string exchangeName = "execution-service-events";
        var routingKey = @event.GetType().Name;

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        using var activity = RabbitMqSagaActivity.StartProducer(routingKey);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", exchangeName);
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("event_type", routingKey);
        activity?.SetTag("aggregate_id", @event.AggregateId.ToString());

        var headers = new Dictionary<string, object?>();
        RabbitMqSagaActivity.Inject(headers);
        var correlationId = headers[RabbitMqSagaActivity.CorrelationHeader]?.ToString();

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event, @event.GetType()));
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = correlationId,
            Headers = headers
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);

        RabbitMqSagaActivity.RecordPublished(routingKey);
        logger?.LogInformation(
            "Saga event published {event_type} {aggregate_id} {correlation_id}",
            routingKey,
            @event.AggregateId,
            correlationId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        _channel = null;
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
