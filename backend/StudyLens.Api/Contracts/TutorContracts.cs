namespace StudyLens.Api.Contracts;

public sealed record ExplainRequest(
    string Question,
    string Language = "bilingual",
    string? ModelId = null);

public sealed record LectureExplainRequest(
    string DocumentId,
    string Language = "bilingual",
    string? ModelId = null);

public sealed record ExerciseExplainRequest(
    string? ExerciseDocumentId,
    string? SolutionDocumentId,
    string Language = "bilingual",
    string? ModelId = null);

public sealed record PracticeRequest(
    string Topic,
    string Difficulty = "exam",
    int QuestionCount = 3,
    string? ModelId = null,
    string? DocumentId = null);

public sealed record GradeRequest(
    string Question,
    string StudentAnswer,
    string? ModelId = null,
    string? DocumentId = null);

public sealed record TutorCitation(
    int Number,
    string ChunkId,
    string DocumentId,
    string Title,
    string RelativePath,
    string MaterialType,
    int Page,
    string Excerpt);

public sealed record ExplainResponse(
    string CourseId,
    string Question,
    string Answer,
    string Model,
    IReadOnlyList<TutorCitation> Citations);

public sealed record PracticeResponse(
    string CourseId,
    string Topic,
    string Model,
    IReadOnlyList<PracticeQuestion> Questions,
    IReadOnlyList<TutorCitation> Citations);

public sealed record PracticeQuestion(
    string Id,
    string Question,
    string CommandWord,
    string Difficulty,
    int MaxScore,
    IReadOnlyList<int> SourceNumbers);

public sealed record GradeResponse(
    string CourseId,
    string Question,
    int Score,
    int MaxScore,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> MissingPoints,
    string ImprovedAnswer,
    string Model,
    IReadOnlyList<TutorCitation> Citations,
    string? AttemptId = null,
    DateTimeOffset? CreatedAtUtc = null);
