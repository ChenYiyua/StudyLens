using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StudyLens.Api.Data;
using StudyLens.Api.Models;

namespace StudyLens.Api.Tests;

public sealed class MongoLearningEventRepositoryTests
{
    [Fact]
    [Trait("Category", "MongoDbIntegration")]
    public async Task Repository_PersistsAcrossInstances_CreatesIndex_AndDeletesByParticipant()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_MONGODB_INTEGRATION_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        const string connectionString = "mongodb://127.0.0.1:27017";
        var databaseName = $"studylens_test_{Guid.NewGuid():N}";
        var options = Options.Create(new MongoDbOptions
        {
            ConnectionString = connectionString,
            DatabaseName = databaseName,
            CollectionName = "learning_events"
        });
        var client = new MongoClient(connectionString);

        try
        {
            var participantId = $"mongo-{Guid.NewGuid():N}";
            var eventToPersist = new LearningEvent
            {
                ParticipantId = participantId,
                Provider = AiProvider.ChatGpt,
                Activity = LearningActivity.Coding,
                StartedAtUtc = DateTime.UtcNow,
                DurationMinutes = 30,
                InteractionCount = 5,
                PromptWordCount = 120,
                HelpfulnessRating = 5
            };

            var writer = new MongoLearningEventRepository(options);
            await writer.AddAsync(eventToPersist);

            var reader = new MongoLearningEventRepository(options);
            var persistedEvents = await reader.GetForParticipantAsync(participantId);
            var indexDocuments = await (await client
                    .GetDatabase(databaseName)
                    .GetCollection<LearningEvent>("learning_events")
                    .Indexes
                    .ListAsync())
                .ToListAsync();

            Assert.Single(persistedEvents);
            Assert.Equal(eventToPersist.Id, persistedEvents[0].Id);
            Assert.Contains(indexDocuments, index =>
                index.TryGetValue("name", out var name) && name.AsString == "participant_started_desc");

            var deleted = await reader.DeleteForParticipantAsync(participantId);
            Assert.Equal(1, deleted);
            Assert.Empty(await reader.GetForParticipantAsync(participantId));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }
}
