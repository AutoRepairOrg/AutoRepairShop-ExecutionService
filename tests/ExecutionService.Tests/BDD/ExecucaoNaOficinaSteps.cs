using ExecutionService.Application.Events;
using ExecutionService.Application.Interfaces;
using ExecutionService.Application.Services;
using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using ExecutionService.Domain.Events;
using ExecutionService.Domain.Interfaces;
using FluentAssertions;
using Reqnroll;

namespace ExecutionService.Tests.BDD;

[Binding]
public class ExecucaoNaOficinaSteps
{
    private readonly InMemoryExecutions _executions = new();
    private readonly RecordingPublisher _publisher = new();
    private readonly OsExecutionStartedHandler _started;
    private readonly OsCancelledHandler _cancelled;
    private readonly ExecutionAppService _app;
    private Guid _osId;

    public ExecucaoNaOficinaSteps()
    {
        _started = new OsExecutionStartedHandler(_executions);
        _cancelled = new OsCancelledHandler(_executions);
        _app = new ExecutionAppService(_executions, _publisher);
    }

    [Given(@"a ordem ""(.*)"" entrou em execução para ""(.*)"" e o veículo ""(.*)""")]
    public async Task DadoOrdemEmExecucao(string osId, string cliente, string placa)
    {
        _osId = Guid.Parse(osId);
        await _started.HandleAsync(_osId, cliente, placa, "Troca de óleo");
    }

    [When(@"o técnico conclui a execução")]
    public async Task QuandoConclui()
    {
        await _app.CompleteAsync(_osId);
    }

    [When(@"o técnico registra a falha ""(.*)""")]
    public async Task QuandoFalha(string motivo)
    {
        await _app.FailAsync(_osId, motivo);
    }

    [When(@"a ordem é cancelada pelo motivo ""(.*)""")]
    public async Task QuandoOrdemCancelada(string motivo)
    {
        await _cancelled.HandleAsync(_osId, motivo);
    }

    [Then(@"a execução está ""(.*)""")]
    public async Task EntaoStatus(string status)
    {
        var execution = await _app.GetByOsIdAsync(_osId);
        execution.Status.Should().Be(status);
    }

    [Then(@"o evento ""(.*)"" foi publicado")]
    public void EntaoEvento(string nome)
    {
        _publisher.Events.Should().Contain(e => e.GetType().Name == nome);
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<DomainEvent> Events { get; } = [];

        public Task PublishAsync(DomainEvent @event, CancellationToken ct = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryExecutions : IExecutionRepository
    {
        private readonly Dictionary<Guid, Execution> _items = [];

        public Task AddAsync(Execution execution, CancellationToken ct = default)
        {
            _items[execution.Id] = execution;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Execution execution, CancellationToken ct = default)
        {
            _items[execution.Id] = execution;
            return Task.CompletedTask;
        }

        public Task<Execution?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task<Execution?> GetByOsIdAsync(Guid osId, CancellationToken ct = default) =>
            Task.FromResult(_items.Values.FirstOrDefault(e => e.OsId == osId));

        public Task<IReadOnlyList<Execution>> ListByStatusAsync(ExecutionStatus status, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Execution>>(_items.Values.Where(e => e.Status == status).ToList());
    }
}
