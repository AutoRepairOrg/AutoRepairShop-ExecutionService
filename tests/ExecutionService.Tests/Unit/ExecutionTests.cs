using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using ExecutionService.Domain.Events;
using FluentAssertions;

namespace ExecutionService.Tests.Unit;

public class ExecutionTests
{
    private static readonly Guid OsId = Guid.NewGuid();

    private static Execution Start() =>
        Execution.Start(OsId, "Maria Silva", "abc1d23", "Troca de pastilha");

    [Fact]
    public void Start_WithValidData_ShouldBeInProgress()
    {
        var execution = Start();

        execution.Status.Should().Be(ExecutionStatus.InProgress);
        execution.OsId.Should().Be(OsId);
        execution.CustomerName.Should().Be("Maria Silva");
        execution.VehiclePlate.Should().Be("ABC1D23");
        execution.Description.Should().Be("Troca de pastilha");
        execution.CompletedAt.Should().BeNull();
        execution.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "ABC1D23")]
    [InlineData("Maria", "")]
    [InlineData(" ", "ABC1D23")]
    public void Start_WithMissingFields_ShouldThrow(string name, string plate)
    {
        var act = () => Execution.Start(OsId, name, plate);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Start_WithEmptyOsId_ShouldThrow()
    {
        var act = () => Execution.Start(Guid.Empty, "Maria", "ABC1D23");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Complete_FromInProgress_ShouldPublishExecutionCompletedEvent()
    {
        var execution = Start();

        execution.Complete();

        execution.Status.Should().Be(ExecutionStatus.Completed);
        execution.CompletedAt.Should().NotBeNull();
        execution.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ExecutionCompletedEvent>()
            .Which.OsId.Should().Be(OsId);
    }

    [Fact]
    public void Complete_WhenNotInProgress_ShouldThrow()
    {
        var execution = Start();
        execution.Complete();

        var act = execution.Complete;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddNotes_ShouldAppendObservations()
    {
        var execution = Start();

        execution.AddNotes("  Peça chegou  ");
        execution.AddNotes("Serviço iniciado");

        execution.Notes.Should().Be($"Peça chegou{Environment.NewLine}Serviço iniciado");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddNotes_WithBlankText_ShouldThrow(string notes)
    {
        var execution = Start();
        var act = () => execution.AddNotes(notes);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddNotes_WhenCompleted_ShouldThrow()
    {
        var execution = Start();
        execution.Complete();

        var act = () => execution.AddNotes("tarde demais");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_FromInProgress_ShouldCancel()
    {
        var execution = Start();

        execution.Cancel("Cliente desistiu");

        execution.Status.Should().Be(ExecutionStatus.Cancelled);
        execution.CancellationReason.Should().Be("Cliente desistiu");
        execution.CancelledAt.Should().NotBeNull();
        execution.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Cancel_WithBlankReason_ShouldThrow(string reason)
    {
        var execution = Start();
        var act = () => execution.Cancel(reason);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldBeIdempotent()
    {
        var execution = Start();
        execution.Cancel("Primeiro motivo");

        execution.Cancel("Segundo motivo");

        execution.CancellationReason.Should().Be("Primeiro motivo");
    }

    [Fact]
    public void Cancel_WhenCompleted_ShouldBeIdempotent()
    {
        var execution = Start();
        execution.Complete();

        execution.Cancel("tarde demais");

        execution.Status.Should().Be(ExecutionStatus.Completed);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyTheList()
    {
        var execution = Start();
        execution.Complete();

        execution.ClearDomainEvents();

        execution.DomainEvents.Should().BeEmpty();
    }
}
