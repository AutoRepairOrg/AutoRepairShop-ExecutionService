using ExecutionService.Application.DTOs;
using ExecutionService.Application.Interfaces;
using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using ExecutionService.Domain.Interfaces;

namespace ExecutionService.Application.Services;

public class ExecutionAppService(
    IExecutionRepository repository,
    IEventPublisher eventPublisher)
{
    public async Task<ExecutionResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var execution = await repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Execução {id} não encontrada.");

        return MapToResponse(execution);
    }

    public async Task<ExecutionResponse> GetByOsIdAsync(Guid osId, CancellationToken ct = default)
    {
        var execution = await repository.GetByOsIdAsync(osId, ct)
            ?? throw new KeyNotFoundException($"Execução para OS {osId} não encontrada.");

        return MapToResponse(execution);
    }

    public async Task<IReadOnlyList<ExecutionResponse>> ListByStatusAsync(string status, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ExecutionStatus>(status, ignoreCase: true, out var parsed))
            throw new ArgumentException($"Status inválido: {status}. Use InProgress, Completed, Cancelled ou Failed.");

        var executions = await repository.ListByStatusAsync(parsed, ct);
        return executions.Select(MapToResponse).ToList();
    }

    public async Task<ExecutionResponse> CompleteAsync(Guid osId, CancellationToken ct = default)
    {
        var execution = await GetByOsOrThrowAsync(osId, ct);
        execution.Complete();
        await repository.UpdateAsync(execution, ct);
        await PublishDomainEventsAsync(execution, ct);
        return MapToResponse(execution);
    }

    public async Task<ExecutionResponse> FailAsync(Guid osId, string reason, CancellationToken ct = default)
    {
        var execution = await GetByOsOrThrowAsync(osId, ct);
        execution.Fail(reason);
        await repository.UpdateAsync(execution, ct);
        await PublishDomainEventsAsync(execution, ct);
        return MapToResponse(execution);
    }

    public async Task<ExecutionResponse> AddNotesAsync(Guid osId, string notes, CancellationToken ct = default)
    {
        var execution = await GetByOsOrThrowAsync(osId, ct);
        execution.AddNotes(notes);
        await repository.UpdateAsync(execution, ct);
        return MapToResponse(execution);
    }

    public async Task<ExecutionResponse> CancelAsync(Guid osId, string reason, CancellationToken ct = default)
    {
        var execution = await GetByOsOrThrowAsync(osId, ct);
        execution.Cancel(reason);
        await repository.UpdateAsync(execution, ct);
        return MapToResponse(execution);
    }

    private async Task<Execution> GetByOsOrThrowAsync(Guid osId, CancellationToken ct)
        => await repository.GetByOsIdAsync(osId, ct)
           ?? throw new KeyNotFoundException($"Execução para OS {osId} não encontrada.");

    private async Task PublishDomainEventsAsync(Execution execution, CancellationToken ct)
    {
        foreach (var @event in execution.DomainEvents)
            await eventPublisher.PublishAsync(@event, ct);

        execution.ClearDomainEvents();
    }

    private static ExecutionResponse MapToResponse(Execution execution) => new(
        execution.Id,
        execution.OsId,
        execution.CustomerName,
        execution.VehiclePlate,
        execution.Description,
        execution.Status.ToString(),
        execution.StartedAt,
        execution.CompletedAt,
        execution.Notes,
        execution.CancelledAt,
        execution.CancellationReason,
        execution.FailedAt,
        execution.FailureReason);
}
