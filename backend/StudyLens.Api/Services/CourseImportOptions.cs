namespace StudyLens.Api.Services;

public sealed class CourseImportOptions
{
    public const string SectionName = "CourseImport";

    public string ImportedCoursesRoot { get; init; } = "App_Data/imported-courses";

    public string? PythonExecutable { get; init; }

    public int TimeoutSeconds { get; init; } = 300;

    public int MaximumFileCount { get; init; } = 100;

    public long MaximumFileBytes { get; init; } = 50 * 1024 * 1024;

    public long MaximumCourseBytes { get; init; } = 250 * 1024 * 1024;
}
