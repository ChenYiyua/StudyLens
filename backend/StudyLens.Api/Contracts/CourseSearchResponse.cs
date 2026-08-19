namespace StudyLens.Api.Contracts;

public sealed record CourseSearchResponse(
    string CourseId,
    string Query,
    int ResultCount,
    IReadOnlyList<CourseSearchResult> Results);

public sealed record CourseSearchResult(
    string ChunkId,
    string DocumentId,
    string Title,
    string RelativePath,
    string MaterialType,
    int Page,
    string Excerpt,
    double RelevanceScore);

public sealed record CourseChunkDetailResponse(
    string CourseId,
    string ChunkId,
    string DocumentId,
    string Title,
    string RelativePath,
    string MaterialType,
    int Page,
    string Text);
