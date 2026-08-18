using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public sealed class MongoLearningEventRepository : ILearningEventRepository
{
    private readonly IMongoCollection<LearningEvent> _collection;
    private readonly Lazy<Task> _indexInitialization;

    public MongoLearningEventRepository(IOptions<MongoDbOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException(
                "MongoDb:ConnectionString must be configured when Storage:Provider is MongoDb.");
        }

        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<LearningEvent>(settings.CollectionName);
        _indexInitialization = new Lazy<Task>(CreateIndexesAsync);
    }

    public async Task AddAsync(
        LearningEvent learningEvent,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        await _collection.InsertOneAsync(learningEvent, cancellationToken: cancellationToken);
    }

    public async Task<LearningEvent?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        var learningEvent = await _collection
            .Find(item => item.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return learningEvent;
    }

    public async Task<IReadOnlyList<LearningEvent>> GetForParticipantAsync(
        string participantId,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        return await _collection
            .Find(item => item.ParticipantId == participantId)
            .SortByDescending(item => item.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> DeleteForParticipantAsync(
        string participantId,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        var result = await _collection.DeleteManyAsync(
            item => item.ParticipantId == participantId,
            cancellationToken);

        return result.DeletedCount;
    }

    private Task EnsureIndexesAsync(CancellationToken cancellationToken) =>
        _indexInitialization.Value.WaitAsync(cancellationToken);

    private async Task CreateIndexesAsync()
    {
        var keys = Builders<LearningEvent>.IndexKeys
            .Ascending(item => item.ParticipantId)
            .Descending(item => item.StartedAtUtc);

        var model = new CreateIndexModel<LearningEvent>(
            keys,
            new CreateIndexOptions { Name = "participant_started_desc" });

        await _collection.Indexes.CreateOneAsync(model);
    }
}
