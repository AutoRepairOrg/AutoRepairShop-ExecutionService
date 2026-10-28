using ExecutionService.Domain.Entities;
using ExecutionService.Domain.Enums;
using ExecutionService.Domain.Interfaces;
using MongoDB.Driver;

namespace ExecutionService.Infrastructure.Persistence;

public class MongoExecutionRepository(IMongoDatabase database) : IExecutionRepository
{
    public const string CollectionName = "executions";

    private readonly IMongoCollection<Execution> _collection = database.GetCollection<Execution>(CollectionName);

    public async Task AddAsync(Execution execution, CancellationToken ct = default)
        => await _collection.InsertOneAsync(execution, cancellationToken: ct);

    public async Task UpdateAsync(Execution execution, CancellationToken ct = default)
        => await _collection.ReplaceOneAsync(e => e.Id == execution.Id, execution, cancellationToken: ct);

    public async Task<Execution?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _collection.Find(e => e.Id == id).FirstOrDefaultAsync(ct);

    public async Task<Execution?> GetByOsIdAsync(Guid osId, CancellationToken ct = default)
        => await _collection.Find(e => e.OsId == osId).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Execution>> ListByStatusAsync(ExecutionStatus status, CancellationToken ct = default)
    {
        var items = await _collection.Find(e => e.Status == status).ToListAsync(ct);
        return items;
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<Execution>(CollectionName);
        var index = new CreateIndexModel<Execution>(
            Builders<Execution>.IndexKeys.Ascending(e => e.OsId),
            new CreateIndexOptions { Unique = true, Name = "ux_execution_osId" });

        await collection.Indexes.CreateOneAsync(index, cancellationToken: ct);
    }
}
