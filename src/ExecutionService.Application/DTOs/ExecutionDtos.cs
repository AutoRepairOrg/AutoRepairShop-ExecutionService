namespace ExecutionService.Application.DTOs;

public record ExecutionResponse(
    Guid Id,
    Guid OsId,
    string CustomerName,
    string VehiclePlate,
    string? Description,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? Notes,
    DateTime? CancelledAt,
    string? CancellationReason);

public record AddNotesRequest(string Notes);

public record CancelExecutionRequest(string Reason);
