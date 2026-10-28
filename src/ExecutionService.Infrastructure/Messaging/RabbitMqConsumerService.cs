using System.Text;
using System.Text.Json;
using ExecutionService.Application.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ExecutionService.Infrastructure.Messaging;

/// <summary>
/// Consome eventos do OsService (Saga coreografada).
/// Exchange: os-service-events → Queue: execution-service-os-events
///   OsExecutionStartedEvent, OsCancelledEvent
/// </summary>
public class RabbitMqConsumerService(
    IConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqConsumerService> logger) : BackgroundService
{
    private const string OsExchange = "os-service-events";
    private const string OsQueue = "execution-service-os-events";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(OsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(OsQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        foreach (var key in new[] { "OsExecutionStartedEvent", "OsCancelledEvent" })
            await _channel.QueueBindAsync(OsQueue, OsExchange, key, cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;

        await _channel.BasicConsumeAsync(OsQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        logger.LogInformation(
            "ExecutionService RabbitMQ consumer iniciado — queue {Queue}, exchange {Exchange}",
            OsQueue, OsExchange);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown normal
        }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var routingKey = ea.RoutingKey;
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            switch (routingKey)
            {
                case "OsExecutionStartedEvent":
                {
                    var msg = JsonSerializer.Deserialize<IncomingOsExecutionStarted>(body, JsonOptions)
                        ?? throw new InvalidOperationException("Payload OsExecutionStartedEvent inválido.");

                    var handler = scope.ServiceProvider.GetRequiredService<OsExecutionStartedHandler>();
                    await handler.HandleAsync(msg.OsId, msg.CustomerName, msg.VehiclePlate, msg.Description);
                    break;
                }
                case "OsCancelledEvent":
                {
                    var msg = JsonSerializer.Deserialize<IncomingOsCancelled>(body, JsonOptions)
                        ?? throw new InvalidOperationException("Payload OsCancelledEvent inválido.");

                    var handler = scope.ServiceProvider.GetRequiredService<OsCancelledHandler>();
                    await handler.HandleAsync(msg.OsId, msg.Reason);
                    break;
                }
                default:
                    logger.LogWarning("Routing key ignorada: {RoutingKey}", routingKey);
                    break;
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            logger.LogInformation("Evento {RoutingKey} processado com sucesso", routingKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao processar {RoutingKey}: {Body}", routingKey, body);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }

    private sealed record IncomingOsExecutionStarted(Guid OsId, string CustomerName, string VehiclePlate, string? Description);
    private sealed record IncomingOsCancelled(Guid OsId, string Reason);
}
