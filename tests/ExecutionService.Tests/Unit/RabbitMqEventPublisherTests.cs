using System.Text;
using System.Text.Json;
using ExecutionService.Domain.Events;
using ExecutionService.Infrastructure.Messaging;
using FluentAssertions;
using Moq;
using RabbitMQ.Client;

namespace ExecutionService.Tests.Unit;

public class RabbitMqEventPublisherTests
{
    private readonly Mock<IConnection> _connection = new();
    private readonly Mock<IChannel> _channel = new(MockBehavior.Loose);

    public RabbitMqEventPublisherTests()
    {
        _channel.SetupGet(c => c.IsOpen).Returns(true);
        _channel
            .Setup(c => c.ExchangeDeclareAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _channel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        _connection
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channel.Object);
    }

    [Fact]
    public async Task PublishAsync_ShouldDeclareExchangeAndPublishJson()
    {
        var sut = new RabbitMqEventPublisher(_connection.Object);
        var osId = Guid.NewGuid();
        var @event = new ExecutionCompletedEvent(osId);

        await sut.PublishAsync(@event);

        _channel.Verify(c => c.ExchangeDeclareAsync(
            "execution-service-events",
            ExchangeType.Topic,
            true,
            false,
            It.IsAny<IDictionary<string, object?>>(),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        var publish = _channel.Invocations.Single(i => i.Method.Name == nameof(IChannel.BasicPublishAsync));
        publish.Arguments[0].Should().Be("execution-service-events");
        publish.Arguments[1].Should().Be(nameof(ExecutionCompletedEvent));
        var body = (ReadOnlyMemory<byte>)publish.Arguments[4]!;
        var json = Encoding.UTF8.GetString(body.Span);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("OsId").GetGuid().Should().Be(osId);
    }

    [Fact]
    public async Task PublishAsync_WhenChannelOpen_ShouldReuseChannel()
    {
        var sut = new RabbitMqEventPublisher(_connection.Object);

        await sut.PublishAsync(new ExecutionCompletedEvent(Guid.NewGuid()));
        await sut.PublishAsync(new ExecutionFailedEvent(Guid.NewGuid(), "erro"));

        _connection.Verify(
            c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenChannelClosed_ShouldCreateNewChannel()
    {
        _channel.SetupGet(c => c.IsOpen).Returns(false);

        var sut = new RabbitMqEventPublisher(_connection.Object);

        await sut.PublishAsync(new ExecutionCompletedEvent(Guid.NewGuid()));
        await sut.PublishAsync(new ExecutionCompletedEvent(Guid.NewGuid()));

        _connection.Verify(
            c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeChannel()
    {
        var sut = new RabbitMqEventPublisher(_connection.Object);
        await sut.PublishAsync(new ExecutionCompletedEvent(Guid.NewGuid()));

        await sut.DisposeAsync();

        _channel.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task Dispose_ShouldDisposeChannel()
    {
        var sut = new RabbitMqEventPublisher(_connection.Object);
        await sut.PublishAsync(new ExecutionCompletedEvent(Guid.NewGuid()));

        sut.Dispose();

        _channel.Verify(c => c.DisposeAsync(), Times.Once);
    }
}
