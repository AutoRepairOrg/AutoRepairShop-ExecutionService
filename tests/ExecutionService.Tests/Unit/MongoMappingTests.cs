using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using ExecutionService.Infrastructure.Persistence;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace ExecutionService.Tests.Unit;

public class MongoMappingTests
{
    [Fact]
    public void Execution_RoundTripsThroughBson()
    {
        MongoMappings.Register();

        var osId = Guid.NewGuid();
        var execution = Execution.Start(osId, "Ana", "abc1234", "Alinhamento");
        execution.AddNotes("Peça em estoque");

        var document = execution.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<Execution>(document);

        restored.Id.Should().Be(execution.Id);
        restored.OsId.Should().Be(osId);
        restored.CustomerName.Should().Be("Ana");
        restored.VehiclePlate.Should().Be("ABC1234");
        restored.Description.Should().Be("Alinhamento");
        restored.Status.Should().Be(ExecutionStatus.InProgress);
        restored.Notes.Should().Be("Peça em estoque");
        restored.StartedAt.Should().BeCloseTo(execution.StartedAt, TimeSpan.FromMilliseconds(1));
        document.Contains("DomainEvents").Should().BeFalse();
        document["Status"].AsString.Should().Be("InProgress");
    }
}
