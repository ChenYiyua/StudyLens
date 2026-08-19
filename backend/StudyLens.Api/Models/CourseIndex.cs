namespace StudyLens.Api.Models;

public sealed record CourseIndex(
    int SchemaVersion,
    CourseInfo Course,
    DateTimeOffset GeneratedAtUtc,
    CourseStatistics Statistics,
    IReadOnlyList<CourseDocument> Documents,
    IReadOnlyList<CourseChunk> Chunks);

public sealed record CourseInfo(string Id, string Name);

public sealed record CourseStatistics(
    int DocumentCount,
    int PageCount,
    int EmptyPageCount,
    int ChunkCount,
    int SkippedDocumentCount = 0);

public sealed record CourseDocument(
    string Id,
    string Title,
    string RelativePath,
    string MaterialType,
    int PageCount,
    int ChunkCount);

public sealed record CourseChunk(
    string Id,
    string DocumentId,
    string Title,
    string RelativePath,
    string MaterialType,
    int Page,
    string Text);
