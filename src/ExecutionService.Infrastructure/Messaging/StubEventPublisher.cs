using ExecutionService.Application.Interfaces;
using ExecutionService.Domain.Events;
using Microsoft.Extensions.Logging;

namespace ExecutionService.Infrastructure.Messaging;

/// <summary>
/// Publisher fake para desenvolvimento local sem RabbitMQ.
/// Ativado via appsettings: "Messaging:UseStub": true
/// </summary>
public class StubEventPublisher(ILogger<StubEventPublisher> logger) : IEventPublisher
{
    public Task PublishAsync(DomainEvent @event, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[STUB EVENT] {EventType} publicado — AggregateId: {Id} — OccurredAt: {At}",
            @event.GetType().Name,
            @event.AggregateId,
            @event.OccurredAt);

        return Task.CompletedTask;
    }
}
