using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using ExecutionService.Infrastructure.Persistence;
using FluentAssertions;
using MongoDB.Driver;
using Moq;

namespace ExecutionService.Tests.Unit;

public class MongoExecutionRepositoryTests
{
    private readonly Mock<IMongoDatabase> _database = new();
    private readonly Mock<IMongoCollection<Execution>> _collection = new();
    private readonly Mock<IMongoIndexManager<Execution>> _indexes = new();
    private readonly MongoExecutionRepository _sut;

    public MongoExecutionRepositoryTests()
    {
        _database
            .Setup(d => d.GetCollection<Execution>(MongoExecutionRepository.CollectionName, It.IsAny<MongoCollectionSettings>()))
            .Returns(_collection.Object);

        _collection.SetupGet(c => c.Indexes).Returns(_indexes.Object);
        _sut = new MongoExecutionRepository(_database.Object);
    }

    [Fact]
    public async Task AddAsync_ShouldInsertDocument()
    {
        var execution = Execution.Start(Guid.NewGuid(), "Ana", "ABC1234");
        _collection
            .Setup(c => c.InsertOneAsync(execution, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.AddAsync(execution);

        _collection.Verify(c => c.InsertOneAsync(
            execution,
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReplaceDocument()
    {
        var execution = Execution.Start(Guid.NewGuid(), "Ana", "ABC1234");
        _collection
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Execution>>(),
                execution,
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ReplaceOneResult>());

        await _sut.UpdateAsync(execution);

        _collection.Verify(c => c.ReplaceOneAsync(
            It.IsAny<FilterDefinition<Execution>>(),
            execution,
            It.IsAny<ReplaceOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ShouldReturnExecution()
    {
        var execution = Execution.Start(Guid.NewGuid(), "Ana", "ABC1234");
        SetupFindCursor(execution);

        var result = await _sut.GetByIdAsync(execution.Id);

        result.Should().BeSameAs(execution);
    }

    [Fact]
    public async Task GetByOsIdAsync_WhenMissing_ShouldReturnNull()
    {
        SetupFindCursor();

        var result = await _sut.GetByOsIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByOsIdAsync_WhenFound_ShouldReturnExecution()
    {
        var execution = Execution.Start(Guid.NewGuid(), "Ana", "ABC1234");
        SetupFindCursor(execution);

        var result = await _sut.GetByOsIdAsync(execution.OsId);

        result.Should().BeSameAs(execution);
    }

    [Fact]
    public async Task ListByStatusAsync_ShouldReturnMatchingItems()
    {
        var execution = Execution.Start(Guid.NewGuid(), "Ana", "ABC1234");
        SetupFindCursor(execution);

        var result = await _sut.ListByStatusAsync(ExecutionStatus.InProgress);

        result.Should().ContainSingle().Which.Should().BeSameAs(execution);
    }

    [Fact]
    public async Task EnsureIndexesAsync_ShouldCreateUniqueOsIdIndex()
    {
        CreateIndexModel<Execution>? created = null;
        _indexes
            .Setup(i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<Execution>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateIndexModel<Execution>, CreateOneIndexOptions?, CancellationToken>((model, _, _) => created = model)
            .ReturnsAsync("ux_execution_osId");

        await MongoExecutionRepository.EnsureIndexesAsync(_database.Object);

        created.Should().NotBeNull();
        created!.Options.Name.Should().Be("ux_execution_osId");
        created.Options.Unique.Should().BeTrue();
    }

    private void SetupFindCursor(params Execution[] items)
    {
        var cursor = new Mock<IAsyncCursor<Execution>>();
        cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns(items);
        cursor.Setup(c => c.Dispose());

        _collection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Execution>>(),
                It.IsAny<FindOptions<Execution, Execution>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
    }
}
