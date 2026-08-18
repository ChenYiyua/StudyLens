using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudyLens.Api.Models;

public sealed class LearningEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; } = ObjectId.GenerateNewId().ToString();

    public required string ParticipantId { get; init; }

    [BsonRepresentation(BsonType.String)]
    public required AiProvider Provider { get; init; }

    [BsonRepresentation(BsonType.String)]
    public required LearningActivity Activity { get; init; }

    public required DateTime StartedAtUtc { get; init; }
    public required int DurationMinutes { get; init; }
    public required int InteractionCount { get; init; }
    public required int PromptWordCount { get; init; }
    public required int HelpfulnessRating { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
