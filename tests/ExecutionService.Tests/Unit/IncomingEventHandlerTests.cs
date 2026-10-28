using ExecutionService.Application.Events;
using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using ExecutionService.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace ExecutionService.Tests.Unit;

public class IncomingEventHandlerTests
{
    [Fact]
    public async Task OsExecutionStartedHandler_ShouldCreateInProgressExecution()
    {
        var repo = new Mock<IExecutionRepository>();
        var osId = Guid.NewGuid();

        repo.Setup(r => r.GetByOsIdAsync(osId, default)).ReturnsAsync((Execution?)null);
        repo.Setup(r => r.AddAsync(It.IsAny<Execution>(), default)).Returns(Task.CompletedTask);

        var handler = new OsExecutionStartedHandler(repo.Object);
        await handler.HandleAsync(osId, "Pedro", "BBB2222", "Alinhamento");

        repo.Verify(r => r.AddAsync(It.Is<Execution>(e =>
            e.OsId == osId &&
            e.Status == ExecutionStatus.InProgress &&
            e.CustomerName == "Pedro" &&
            e.VehiclePlate == "BBB2222" &&
            e.Description == "Alinhamento"), default), Times.Once);
    }

    [Fact]
    public async Task OsExecutionStartedHandler_WhenExecutionExists_ShouldBeIdempotent()
    {
        var repo = new Mock<IExecutionRepository>();
        var osId = Guid.NewGuid();
        var existing = Execution.Start(osId, "Pedro", "BBB2222");

        repo.Setup(r => r.GetByOsIdAsync(osId, default)).ReturnsAsync(existing);

        var handler = new OsExecutionStartedHandler(repo.Object);
        await handler.HandleAsync(osId, "Pedro", "BBB2222", null);

        repo.Verify(r => r.AddAsync(It.IsAny<Execution>(), default), Times.Never);
    }

    [Fact]
    public async Task OsCancelledHandler_ShouldCancelInProgressExecution()
    {
        var repo = new Mock<IExecutionRepository>();
        var osId = Guid.NewGuid();
        var execution = Execution.Start(osId, "Pedro", "BBB2222");

        repo.Setup(r => r.GetByOsIdAsync(osId, default)).ReturnsAsync(execution);
        repo.Setup(r => r.UpdateAsync(execution, default)).Returns(Task.CompletedTask);

        var handler = new OsCancelledHandler(repo.Object);
        await handler.HandleAsync(osId, "Cliente desistiu");

        execution.Status.Should().Be(ExecutionStatus.Cancelled);
        execution.CancellationReason.Should().Be("OS cancelada: Cliente desistiu");
        repo.Verify(r => r.UpdateAsync(execution, default), Times.Once);
    }

    [Fact]
    public async Task OsCancelledHandler_WhenMissing_ShouldNotThrow()
    {
        var repo = new Mock<IExecutionRepository>();
        var osId = Guid.NewGuid();
        repo.Setup(r => r.GetByOsIdAsync(osId, default)).ReturnsAsync((Execution?)null);

        var handler = new OsCancelledHandler(repo.Object);
        var act = async () => await handler.HandleAsync(osId, "Motivo");

        await act.Should().NotThrowAsync();
        repo.Verify(r => r.UpdateAsync(It.IsAny<Execution>(), default), Times.Never);
    }
}
