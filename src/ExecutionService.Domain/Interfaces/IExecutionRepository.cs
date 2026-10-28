using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;

namespace ExecutionService.Domain.Interfaces;

public interface IExecutionRepository
{
    Task AddAsync(Execution execution, CancellationToken ct = default);
    Task UpdateAsync(Execution execution, CancellationToken ct = default);
    Task<Execution?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Execution?> GetByOsIdAsync(Guid osId, CancellationToken ct = default);
    Task<IReadOnlyList<Execution>> ListByStatusAsync(ExecutionStatus status, CancellationToken ct = default);
}
