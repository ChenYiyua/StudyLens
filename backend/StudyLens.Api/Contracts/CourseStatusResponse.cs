namespace StudyLens.Api.Contracts;

public sealed record CourseStatusResponse(
    string CourseId,
    bool Ready,
    bool SourceAvailable,
    string? CourseName,
    DateTimeOffset? GeneratedAtUtc,
    int DocumentCount,
    int PageCount,
    int EmptyPageCount,
    int ChunkCount,
    int SkippedDocumentCount,
    IReadOnlyDictionary<string, int> MaterialTypes,
    string? Message);

public sealed record CourseCatalogResponse(
    string DefaultCourseId,
    IReadOnlyList<CourseStatusResponse> Courses);
