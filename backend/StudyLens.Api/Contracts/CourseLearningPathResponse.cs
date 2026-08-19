namespace StudyLens.Api.Contracts;

public sealed record CourseLearningPathResponse(
    string CourseId,
    IReadOnlyList<CourseMaterialResponse> Lectures,
    IReadOnlyList<CourseExerciseUnitResponse> Exercises,
    IReadOnlyList<CourseMaterialResponse> OtherMaterials);

public sealed record CourseMaterialResponse(
    string DocumentId,
    string Title,
    string RelativePath,
    string MaterialType,
    int PageCount,
    int ChunkCount);

public sealed record CourseExerciseUnitResponse(
    string Id,
    string Title,
    CourseMaterialResponse? Exercise,
    CourseMaterialResponse? Solution);
