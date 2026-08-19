namespace StudyLens.Api.Data;

public sealed class CourseCatalogOptions
{
    public const string SectionName = "CourseCatalog";

    public string DefaultCourseId { get; init; } = "studylens-demo";

    public IReadOnlyList<CourseSourceOptions> Courses { get; init; } = [];

    public string ImportedCoursesRoot { get; init; } = "App_Data/imported-courses";
}

public sealed class CourseSourceOptions
{
    public required string Id { get; init; }

    public required string IndexPath { get; init; }

    public string? SourceRoot { get; init; }
}
