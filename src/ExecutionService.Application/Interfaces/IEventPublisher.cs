using ExecutionService.Domain.Events;

namespace ExecutionService.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(DomainEvent @event, CancellationToken ct = default);
}
