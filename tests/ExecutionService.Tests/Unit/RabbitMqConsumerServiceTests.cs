using System.Text;
using System.Text.Json;
using ExecutionService.Application.Events;
using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Interfaces;
using ExecutionService.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace ExecutionService.Tests.Unit;

public class RabbitMqConsumerServiceTests
{
    private readonly Mock<IConnection> _connection = new();
    private readonly Mock<IChannel> _channel = new();
    private readonly Mock<IExecutionRepository> _repository = new();
    private readonly Mock<ILogger<RabbitMqConsumerService>> _logger = new();
    private readonly TaskCompletionSource _consumeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IAsyncBasicConsumer? _consumer;

    public RabbitMqConsumerServiceTests()
    {
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

        _channel
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("execution-service-os-events", 0, 0));

        _channel
            .Setup(c => c.QueueBindAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _channel
            .Setup(c => c.BasicQosAsync(It.IsAny<uint>(), It.IsAny<ushort>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _channel
            .Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _channel
            .Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _channel.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);

        _channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()))
            .Callback(new InvocationAction(invocation =>
            {
                _consumer = (IAsyncBasicConsumer)invocation.Arguments[6]!;
                _consumeStarted.TrySetResult();
            }))
            .ReturnsAsync("ctag");

        _connection
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channel.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeclareTopologyAndConsume()
    {
        await using var provider = BuildProvider();
        var sut = CreateSut(provider);
        using var cts = new CancellationTokenSource();

        var run = sut.RunAsync(cts.Token);
        await _consumeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await run;

        _channel.Verify(c => c.ExchangeDeclareAsync(
            "os-service-events",
            ExchangeType.Topic,
            true,
            false,
            It.IsAny<IDictionary<string, object?>>(),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        _channel.Verify(c => c.QueueDeclareAsync(
            "execution-service-os-events",
            true,
            false,
            false,
            It.IsAny<IDictionary<string, object?>>(),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        _channel.Verify(c => c.QueueBindAsync(
            "execution-service-os-events",
            "os-service-events",
            "OsExecutionStartedEvent",
            It.IsAny<IDictionary<string, object?>>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        _channel.Verify(c => c.QueueBindAsync(
            "execution-service-os-events",
            "os-service-events",
            "OsCancelledEvent",
            It.IsAny<IDictionary<string, object?>>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        _channel.Verify(c => c.BasicQosAsync(0, 1, false, It.IsAny<CancellationToken>()), Times.Once);
        _channel.Verify(c => c.BasicConsumeAsync(
            "execution-service-os-events",
            false,
            It.IsAny<string>(),
            false,
            false,
            It.IsAny<IDictionary<string, object?>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnReceived_OsExecutionStarted_ShouldHandleAndAck()
    {
        var osId = Guid.NewGuid();
        _repository.Setup(r => r.GetByOsIdAsync(osId, It.IsAny<CancellationToken>())).ReturnsAsync((Execution?)null);
        _repository.Setup(r => r.AddAsync(It.IsAny<Execution>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await using var provider = BuildProvider();
        var sut = CreateSut(provider);
        using var cts = new CancellationTokenSource();
        var run = sut.RunAsync(cts.Token);
        await _consumeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var body = JsonSerializer.Serialize(new
        {
            OsId = osId,
            CustomerName = "Ana",
            VehiclePlate = "ABC1234",
            Description = "Alinhamento"
        });

        await DeliverAsync("OsExecutionStartedEvent", body);

        _repository.Verify(r => r.AddAsync(
            It.Is<Execution>(e => e.OsId == osId && e.CustomerName == "Ana"),
            It.IsAny<CancellationToken>()), Times.Once);
        _channel.Verify(c => c.BasicAckAsync(42UL, false, It.IsAny<CancellationToken>()), Times.Once);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task OnReceived_OsCancelled_ShouldHandleAndAck()
    {
        var osId = Guid.NewGuid();
        var execution = Execution.Start(osId, "Ana", "ABC1234");
        _repository.Setup(r => r.GetByOsIdAsync(osId, It.IsAny<CancellationToken>())).ReturnsAsync(execution);
        _repository.Setup(r => r.UpdateAsync(execution, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await using var provider = BuildProvider();
        var sut = CreateSut(provider);
        using var cts = new CancellationTokenSource();
        var run = sut.RunAsync(cts.Token);
        await _consumeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var body = JsonSerializer.Serialize(new { OsId = osId, Reason = "cliente desistiu" });
        await DeliverAsync("OsCancelledEvent", body);

        _repository.Verify(r => r.UpdateAsync(execution, It.IsAny<CancellationToken>()), Times.Once);
        _channel.Verify(c => c.BasicAckAsync(42UL, false, It.IsAny<CancellationToken>()), Times.Once);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task OnReceived_UnknownRoutingKey_ShouldAck()
    {
        await using var provider = BuildProvider();
        var sut = CreateSut(provider);
        using var cts = new CancellationTokenSource();
        var run = sut.RunAsync(cts.Token);
        await _consumeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await DeliverAsync("UnknownEvent", "{}");

        _channel.Verify(c => c.BasicAckAsync(42UL, false, It.IsAny<CancellationToken>()), Times.Once);
        _channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task OnReceived_InvalidPayload_ShouldNackWithoutRequeue()
    {
        await using var provider = BuildProvider();
        var sut = CreateSut(provider);
        using var cts = new CancellationTokenSource();
        var run = sut.RunAsync(cts.Token);
        await _consumeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await DeliverAsync("OsExecutionStartedEvent", "null");

        _channel.Verify(c => c.BasicNackAsync(42UL, false, false, It.IsAny<CancellationToken>()), Times.Once);
        _channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task OnReceived_WhenHandlerThrows_ShouldNackWithoutRequeue()
    {
        var osId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByOsIdAsync(osId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await using var provider = BuildProvider();
        var sut = CreateSut(provider);
        using var cts = new CancellationTokenSource();
        var run = sut.RunAsync(cts.Token);
        await _consumeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var body = JsonSerializer.Serialize(new
        {
            OsId = osId,
            CustomerName = "Ana",
            VehiclePlate = "ABC1234",
            Description = (string?)null
        });

        await DeliverAsync("OsExecutionStartedEvent", body);

        _channel.Verify(c => c.BasicNackAsync(42UL, false, false, It.IsAny<CancellationToken>()), Times.Once);

        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public async Task StopAsync_WhenChannelExists_ShouldDisposeChannel()
    {
        await using var provider = BuildProvider();
        var sut = CreateSut(provider);
        using var cts = new CancellationTokenSource();
        var run = sut.RunAsync(cts.Token);
        await _consumeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await sut.StopAsync(CancellationToken.None);
        await cts.CancelAsync();
        await run;

        _channel.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenChannelNull_ShouldNotThrow()
    {
        await using var provider = BuildProvider();
        var sut = CreateSut(provider);

        var act = async () => await sut.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private TestableRabbitMqConsumerService CreateSut(ServiceProvider provider)
        => new(_connection.Object, provider.GetRequiredService<IServiceScopeFactory>(), _logger.Object);

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_repository.Object);
        services.AddScoped<OsExecutionStartedHandler>();
        services.AddScoped<OsCancelledHandler>();
        return services.BuildServiceProvider();
    }

    private async Task DeliverAsync(string routingKey, string body)
    {
        _consumer.Should().NotBeNull();
        await _consumer!.HandleBasicDeliverAsync(
            consumerTag: "ctag",
            deliveryTag: 42UL,
            redelivered: false,
            exchange: "os-service-events",
            routingKey: routingKey,
            properties: Mock.Of<IReadOnlyBasicProperties>(),
            body: Encoding.UTF8.GetBytes(body));
    }

    private sealed class TestableRabbitMqConsumerService(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerService> logger)
        : RabbitMqConsumerService(connection, scopeFactory, logger)
    {
        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }
}
