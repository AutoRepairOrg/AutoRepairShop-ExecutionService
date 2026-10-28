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

/// <summary>
/// Publicado quando a execução falha na oficina.
/// Consumido pelo OsService (cancela a OS e publica OsCancelledEvent).
/// </summary>
public record ExecutionFailedEvent(Guid OsId, string Reason)
    : DomainEvent(OsId);
