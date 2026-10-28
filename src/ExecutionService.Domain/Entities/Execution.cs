using ExecutionService.Domain.Enums;
using ExecutionService.Domain.Events;

namespace ExecutionService.Domain.Entities;

/// <summary>
/// Aggregate root da execução na oficina.
/// Nasce ao consumir OsExecutionStartedEvent e termina quando o técnico conclui o serviço.
/// </summary>
public class Execution
{
    public Guid Id { get; private set; }
    public Guid OsId { get; private set; }
    public string CustomerName { get; private set; }
    public string VehiclePlate { get; private set; }
    public string? Description { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    private readonly List<DomainEvent> _domainEvents = [];

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

#pragma warning disable CS8618
    private Execution() { }
#pragma warning restore CS8618

    private Execution(Guid osId, string customerName, string vehiclePlate, string? description)
    {
        Id = Guid.NewGuid();
        OsId = osId;
        CustomerName = customerName;
        VehiclePlate = vehiclePlate.ToUpperInvariant();
        Description = description;
        Status = ExecutionStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }

    public static Execution Start(Guid osId, string customerName, string vehiclePlate, string? description = null)
    {
        if (osId == Guid.Empty) throw new ArgumentException("OsId é obrigatório.", nameof(osId));
        ArgumentException.ThrowIfNullOrWhiteSpace(customerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(vehiclePlate);

        return new Execution(osId, customerName, vehiclePlate, description);
    }

    /// <summary>
    /// Técnico conclui o serviço e publica ExecutionCompletedEvent.
    /// </summary>
    public void Complete()
    {
        EnsureStatus(ExecutionStatus.InProgress, "Somente execuções InProgress podem ser concluídas.");
        Status = ExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new ExecutionCompletedEvent(OsId));
    }

    public void AddNotes(string notes)
    {
        EnsureStatus(ExecutionStatus.InProgress, "Observações só podem ser adicionadas em execuções InProgress.");
        ArgumentException.ThrowIfNullOrWhiteSpace(notes);

        var trimmed = notes.Trim();
        Notes = string.IsNullOrWhiteSpace(Notes) ? trimmed : $"{Notes}{Environment.NewLine}{trimmed}";
    }

    /// <summary>
    /// Compensação do Saga (OsCancelledEvent) ou override manual.
    /// Idempotente se já estiver Cancelled ou Completed.
    /// </summary>
    public void Cancel(string reason)
    {
        if (Status is ExecutionStatus.Cancelled or ExecutionStatus.Completed)
            return;

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Status = ExecutionStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void EnsureStatus(ExecutionStatus expected, string message)
    {
        if (Status != expected)
            throw new InvalidOperationException(message);
    }

    private void AddDomainEvent(DomainEvent @event) => _domainEvents.Add(@event);
}
