using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public sealed class MongoLearningEventRepository : ILearningEventRepository
{
    private readonly IMongoCollection<LearningEvent> _collection;

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
    }

    public Task AddAsync(LearningEvent learningEvent, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(learningEvent, cancellationToken: cancellationToken);

    public async Task<LearningEvent?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var learningEvent = await _collection
            .Find(item => item.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return learningEvent;
    }

    public async Task<IReadOnlyList<LearningEvent>> GetForParticipantAsync(
        string participantId,
        CancellationToken cancellationToken = default) =>
        await _collection
            .Find(item => item.ParticipantId == participantId)
            .SortByDescending(item => item.StartedAtUtc)
            .ToListAsync(cancellationToken);
}
