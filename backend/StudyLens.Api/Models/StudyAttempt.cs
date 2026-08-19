namespace StudyLens.Api.Models;

public sealed record StudyAttempt(
    string Id,
    string CourseId,
    string Question,
    string StudentAnswer,
    int Score,
    int MaxScore,
    string Summary,
    string[] Strengths,
    string[] MissingPoints,
    string ImprovedAnswer,
    string Model,
    StudyAttemptCitation[] Citations,
    DateTimeOffset CreatedAtUtc);

public sealed record StudyAttemptCitation(
    int Number,
    string ChunkId,
    string DocumentId,
    string Title,
    string RelativePath,
    string MaterialType,
    string Excerpt,
    int Page);
