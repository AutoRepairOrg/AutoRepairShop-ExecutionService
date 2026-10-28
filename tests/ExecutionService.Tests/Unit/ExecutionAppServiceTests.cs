using ExecutionService.Application.Interfaces;
using ExecutionService.Application.Services;
using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using ExecutionService.Domain.Events;
using ExecutionService.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace ExecutionService.Tests.Unit;

public class ExecutionAppServiceTests
{
    private readonly Mock<IExecutionRepository> _repo = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly ExecutionAppService _sut;

    public ExecutionAppServiceTests()
    {
        _sut = new ExecutionAppService(_repo.Object, _publisher.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ShouldReturnResponse()
    {
        var execution = Execution.Start(Guid.NewGuid(), "Ana", "XYZ1234");
        _repo.Setup(r => r.GetByIdAsync(execution.Id, default)).ReturnsAsync(execution);

        var result = await _sut.GetByIdAsync(execution.Id);

        result.Id.Should().Be(execution.Id);
        result.Status.Should().Be("InProgress");
        result.VehiclePlate.Should().Be("XYZ1234");
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ShouldThrow()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Execution?)null);

        var act = async () => await _sut.GetByIdAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByOsIdAsync_WhenMissing_ShouldThrow()
    {
        _repo.Setup(r => r.GetByOsIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Execution?)null);

        var act = async () => await _sut.GetByOsIdAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ListByStatusAsync_ShouldFilter()
    {
        var execution = Execution.Start(Guid.NewGuid(), "Ana", "XYZ1234");
        _repo.Setup(r => r.ListByStatusAsync(ExecutionStatus.InProgress, default))
            .ReturnsAsync(new List<Execution> { execution });

        var result = await _sut.ListByStatusAsync("inprogress");

        result.Should().ContainSingle();
        result[0].OsId.Should().Be(execution.OsId);
    }

    [Fact]
    public async Task ListByStatusAsync_WithInvalidStatus_ShouldThrow()
    {
        var act = async () => await _sut.ListByStatusAsync("foo");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CompleteAsync_ShouldPersistAndPublish()
    {
        var osId = Guid.NewGuid();
        var execution = Execution.Start(osId, "Ana", "XYZ1234");
        _repo.Setup(r => r.GetByOsIdAsync(osId, default)).ReturnsAsync(execution);
        _repo.Setup(r => r.UpdateAsync(execution, default)).Returns(Task.CompletedTask);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<DomainEvent>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.CompleteAsync(osId);

        result.Status.Should().Be("Completed");
        execution.DomainEvents.Should().BeEmpty();
        _repo.Verify(r => r.UpdateAsync(execution, default), Times.Once);
        _publisher.Verify(p => p.PublishAsync(It.Is<ExecutionCompletedEvent>(e => e.OsId == osId), default), Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_WhenMissing_ShouldThrow()
    {
        _repo.Setup(r => r.GetByOsIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Execution?)null);

        var act = async () => await _sut.CompleteAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddNotesAsync_ShouldUpdate()
    {
        var osId = Guid.NewGuid();
        var execution = Execution.Start(osId, "Ana", "XYZ1234");
        _repo.Setup(r => r.GetByOsIdAsync(osId, default)).ReturnsAsync(execution);
        _repo.Setup(r => r.UpdateAsync(execution, default)).Returns(Task.CompletedTask);

        var result = await _sut.AddNotesAsync(osId, "Aguardando peça");

        result.Notes.Should().Be("Aguardando peça");
        _repo.Verify(r => r.UpdateAsync(execution, default), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ShouldCancel()
    {
        var osId = Guid.NewGuid();
        var execution = Execution.Start(osId, "Ana", "XYZ1234");
        _repo.Setup(r => r.GetByOsIdAsync(osId, default)).ReturnsAsync(execution);
        _repo.Setup(r => r.UpdateAsync(execution, default)).Returns(Task.CompletedTask);

        var result = await _sut.CancelAsync(osId, "Override do técnico");

        result.Status.Should().Be("Cancelled");
        result.CancellationReason.Should().Be("Override do técnico");
    }

    [Fact]
    public async Task FailAsync_ShouldPersistAndPublishEvent()
    {
        var osId = Guid.NewGuid();
        var execution = Execution.Start(osId, "Ana", "XYZ1234");
        _repo.Setup(r => r.GetByOsIdAsync(osId, default)).ReturnsAsync(execution);
        _repo.Setup(r => r.UpdateAsync(execution, default)).Returns(Task.CompletedTask);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<DomainEvent>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.FailAsync(osId, "Peça indisponível");

        result.Status.Should().Be("Failed");
        result.FailureReason.Should().Be("Peça indisponível");
        result.FailedAt.Should().NotBeNull();
        execution.DomainEvents.Should().BeEmpty();
        _repo.Verify(r => r.UpdateAsync(execution, default), Times.Once);
        _publisher.Verify(p => p.PublishAsync(
            It.Is<ExecutionFailedEvent>(e => e.OsId == osId && e.Reason == "Peça indisponível"),
            default), Times.Once);
    }

    [Fact]
    public async Task FailAsync_WhenNotFound_ShouldThrow()
    {
        _repo.Setup(r => r.GetByOsIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Execution?)null);

        var act = async () => await _sut.FailAsync(Guid.NewGuid(), "Motivo");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
