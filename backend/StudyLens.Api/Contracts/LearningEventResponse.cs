using StudyLens.Api.Models;

namespace StudyLens.Api.Contracts;

public sealed record LearningEventResponse(
    string Id,
    string ParticipantId,
    AiProvider Provider,
    LearningActivity Activity,
    DateTime StartedAtUtc,
    int DurationMinutes,
    int InteractionCount,
    int PromptWordCount,
    int HelpfulnessRating);
