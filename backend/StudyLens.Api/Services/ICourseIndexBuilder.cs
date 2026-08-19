namespace StudyLens.Api.Services;

public interface ICourseIndexBuilder
{
    Task BuildAsync(
        string sourcePath,
        string outputPath,
        string courseId,
        string courseName,
        CancellationToken cancellationToken);
}
