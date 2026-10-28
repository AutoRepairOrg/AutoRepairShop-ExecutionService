using ExecutionService.Application.DTOs;
using ExecutionService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExecutionService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExecutionController(ExecutionAppService appService) : ControllerBase
{
    /// <summary>Busca execução por ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await appService.GetByIdAsync(id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Busca a execução da OS.</summary>
    [HttpGet("os/{osId:guid}")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByOsId(Guid osId, CancellationToken ct)
    {
        try
        {
            return Ok(await appService.GetByOsIdAsync(osId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Fila de execuções por status (InProgress, Completed, Cancelled, Failed).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExecutionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string status, CancellationToken ct)
    {
        try
        {
            return Ok(await appService.ListByStatusAsync(status, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Técnico conclui o serviço e publica ExecutionCompletedEvent.
    /// </summary>
    [HttpPatch("os/{osId:guid}/complete")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid osId, CancellationToken ct)
    {
        try
        {
            return Ok(await appService.CompleteAsync(osId, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Técnico registra falha na oficina e publica ExecutionFailedEvent.
    /// OsService cancela a OS e dispara a compensação via OsCancelledEvent.
    /// </summary>
    [HttpPatch("os/{osId:guid}/fail")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Fail(Guid osId, [FromBody] FailExecutionRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await appService.FailAsync(osId, request.Reason, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Adiciona observação do técnico enquanto a execução está InProgress.</summary>
    [HttpPatch("os/{osId:guid}/notes")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddNotes(Guid osId, [FromBody] AddNotesRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await appService.AddNotesAsync(osId, request.Notes, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Override manual de cancelamento. O cancelamento normal vem de OsCancelledEvent.</summary>
    [HttpPatch("os/{osId:guid}/cancel")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid osId, [FromBody] CancelExecutionRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await appService.CancelAsync(osId, request.Reason, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
