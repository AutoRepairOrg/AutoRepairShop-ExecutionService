using ExecutionService.Application.Interfaces;
using ExecutionService.Domain.Interfaces;

namespace ExecutionService.Application.Events;

/// <summary>
/// Handlers para eventos consumidos do broker (Saga coreografada).
/// </summary>
public class OsExecutionStartedHandler(IExecutionRepository repository)
{
    /// <summary>
    /// Consumido quando OsService inicia a execução após aprovação do orçamento.
    /// Cria a execução em InProgress. Idempotente se já existir para a OS.
    /// </summary>
    public async Task HandleAsync(
        Guid osId,
        string customerName,
        string vehiclePlate,
        string? description,
        CancellationToken ct = default)
    {
        var existing = await repository.GetByOsIdAsync(osId, ct);
        if (existing is not null)
            return;

        var execution = Domain.Entities.Execution.Start(osId, customerName, vehiclePlate, description);
        await repository.AddAsync(execution, ct);
    }
}

public class OsCancelledHandler(IExecutionRepository repository)
{
    /// <summary>
    /// Compensação do Saga: OS cancelada → cancela a execução em andamento.
    /// Não faz nada se a execução não existir ou já estiver concluída/cancelada.
    /// </summary>
    public async Task HandleAsync(Guid osId, string reason, CancellationToken ct = default)
    {
        var execution = await repository.GetByOsIdAsync(osId, ct);
        if (execution is null)
            return;

        execution.Cancel($"OS cancelada: {reason}");
        await repository.UpdateAsync(execution, ct);
    }
}
