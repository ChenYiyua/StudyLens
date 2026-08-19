using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public sealed class MongoStudyAttemptRepository : IStudyAttemptRepository
{
    private readonly IMongoDatabase database;
    private readonly IMongoCollection<StudyAttempt> collection;
    private readonly Lazy<Task> indexInitialization;

    public MongoStudyAttemptRepository(IOptions<MongoDbOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException(
                "MongoDb:ConnectionString must be configured when StudyData:Provider is MongoDb.");
        }

        var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
        clientSettings.ConnectTimeout = TimeSpan.FromSeconds(3);
        var client = new MongoClient(clientSettings);
        database = client.GetDatabase(settings.DatabaseName);
        collection = database.GetCollection<StudyAttempt>(settings.CollectionName);
        indexInitialization = new Lazy<Task>(CreateIndexesAsync);
    }

    public async Task<StudyDataStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return new StudyDataStatus(
                "MongoDb",
                true,
                $"Connected to database '{database.DatabaseNamespace.DatabaseName}'.");
        }
        catch (Exception exception) when (
            exception is MongoException or TimeoutException or OperationCanceledException)
        {
            return new StudyDataStatus(
                "MongoDb",
                false,
                $"MongoDB is unavailable: {exception.Message}");
        }
    }

    public async Task AddAsync(StudyAttempt attempt, CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        await collection.InsertOneAsync(attempt, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<StudyAttempt>> GetForCourseAsync(
        string courseId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        return await collection
            .Find(attempt => attempt.CourseId == courseId)
            .SortByDescending(attempt => attempt.CreatedAtUtc)
            .Limit(Math.Clamp(limit, 1, 5000))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> DeleteForCourseAsync(
        string courseId,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        var result = await collection.DeleteManyAsync(
            attempt => attempt.CourseId == courseId,
            cancellationToken);
        return result.DeletedCount;
    }

    private Task EnsureIndexesAsync(CancellationToken cancellationToken) =>
        indexInitialization.Value.WaitAsync(cancellationToken);

    private async Task CreateIndexesAsync()
    {
        var keys = Builders<StudyAttempt>.IndexKeys
            .Ascending(attempt => attempt.CourseId)
            .Descending(attempt => attempt.CreatedAtUtc);
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<StudyAttempt>(
            keys,
            new CreateIndexOptions { Name = "course_created_desc" }));
    }
}
