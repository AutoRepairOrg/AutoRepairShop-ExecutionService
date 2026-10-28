namespace ExecutionService.Domain.Events;

public abstract record DomainEvent(Guid AggregateId, DateTime OccurredAt)
{
    protected DomainEvent(Guid aggregateId) : this(aggregateId, DateTime.UtcNow) { }
}

/// <summary>
/// Publicado quando o técnico conclui a execução.
/// Consumido pelo BillingService (abre checkout) e pelo OsService (Finish).
/// </summary>
public record ExecutionCompletedEvent(Guid OsId)
    : DomainEvent(OsId);
