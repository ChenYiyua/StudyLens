namespace StudyLens.Api.Contracts;

public sealed record StudyHistoryResponse(
    string CourseId,
    int AttemptCount,
    double? AveragePercentage,
    DateTimeOffset? LatestAttemptAtUtc,
    IReadOnlyList<StudyAttemptResponse> Attempts);

public sealed record StudyAttemptResponse(
    string Id,
    string CourseId,
    string Question,
    string StudentAnswer,
    int Score,
    int MaxScore,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> MissingPoints,
    string ImprovedAnswer,
    string Model,
    IReadOnlyList<StudyAttemptCitationResponse> Citations,
    DateTimeOffset CreatedAtUtc);

public sealed record StudyAttemptCitationResponse(
    int Number,
    string ChunkId,
    string DocumentId,
    string Title,
    string RelativePath,
    string MaterialType,
    string Excerpt,
    int Page);

public sealed record DeleteStudyHistoryResponse(string CourseId, long DeletedCount);
