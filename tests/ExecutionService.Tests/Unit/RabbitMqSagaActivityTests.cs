using System.Diagnostics;
using System.Text;
using ExecutionService.Infrastructure.Messaging;
using FluentAssertions;
using RabbitMQ.Client;

namespace ExecutionService.Tests.Unit;

public class RabbitMqSagaActivityTests
{
    [Fact]
    public void Begin_ShouldRestorePreviousCorrelation()
    {
        using (RabbitMqSagaActivity.Begin("outer"))
        {
            using (RabbitMqSagaActivity.Begin("inner"))
                RabbitMqSagaActivity.Current.Should().Be("inner");

            RabbitMqSagaActivity.Current.Should().Be("outer");
        }

        RabbitMqSagaActivity.Current.Should().BeNull();
    }

    [Fact]
    public void Extract_ShouldPreferHeaderBytesOverCorrelationId()
    {
        var properties = new BasicProperties
        {
            CorrelationId = "from-property",
            Headers = new Dictionary<string, object?>
            {
                [RabbitMqSagaActivity.CorrelationHeader] = Encoding.UTF8.GetBytes("from-header")
            }
        };

        RabbitMqSagaActivity.Extract(properties).Should().Be("from-header");
    }

    [Fact]
    public void Extract_ShouldUseCorrelationIdWhenHeaderIsMissing()
    {
        var properties = new BasicProperties { CorrelationId = "from-property" };

        RabbitMqSagaActivity.Extract(properties).Should().Be("from-property");
    }

    [Fact]
    public void Extract_ShouldCreateIdWhenMessageHasNoCorrelation()
    {
        RabbitMqSagaActivity.Extract(new BasicProperties()).Should().HaveLength(32);
    }

    [Fact]
    public void Inject_ShouldWriteCorrelationAndTraceParent()
    {
        using var listener = Listen();
        using var scope = RabbitMqSagaActivity.Begin("corr-os-1");
        var headers = new Dictionary<string, object?>();

        using (var activity = RabbitMqSagaActivity.StartProducer("ExecutionCompletedEvent"))
        {
            activity.Should().NotBeNull();
            RabbitMqSagaActivity.Inject(headers);
        }

        headers[RabbitMqSagaActivity.CorrelationHeader].Should().Be("corr-os-1");
        headers[RabbitMqSagaActivity.TraceParentHeader]!.ToString().Should().StartWith("00-");
    }

    [Fact]
    public void StartConsumer_ShouldContinueTraceFromParentHeader()
    {
        using var listener = Listen();
        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                [RabbitMqSagaActivity.CorrelationHeader] = "abc",
                [RabbitMqSagaActivity.TraceParentHeader] =
                    "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
            }
        };

        using var activity = RabbitMqSagaActivity.StartConsumer("OsExecutionStartedEvent", properties);

        activity.Should().NotBeNull();
        activity!.ParentSpanId.ToString().Should().Be("b7ad6b7169203331");
        RabbitMqSagaActivity.RecordPublished("OsExecutionStartedEvent");
        RabbitMqSagaActivity.RecordConsumed("OsExecutionStartedEvent");
        RabbitMqSagaActivity.RecordFailed("OsExecutionStartedEvent");
    }

    private static ActivityListener Listen()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RabbitMqSagaActivity.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
