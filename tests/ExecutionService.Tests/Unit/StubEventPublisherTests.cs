using ExecutionService.Domain.Events;
using ExecutionService.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExecutionService.Tests.Unit;

public class StubEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldCompleteWithoutThrowing()
    {
        var logger = new Mock<ILogger<StubEventPublisher>>();
        var sut = new StubEventPublisher(logger.Object);
        var @event = new ExecutionCompletedEvent(Guid.NewGuid());

        var act = async () => await sut.PublishAsync(@event);

        await act.Should().NotThrowAsync();
    }
}
