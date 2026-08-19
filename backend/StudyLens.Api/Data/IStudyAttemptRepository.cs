using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public interface IStudyAttemptRepository
{
    Task<StudyDataStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task AddAsync(StudyAttempt attempt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudyAttempt>> GetForCourseAsync(
        string courseId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<long> DeleteForCourseAsync(string courseId, CancellationToken cancellationToken = default);
}

public sealed record StudyDataStatus(
    string Provider,
    bool Available,
    string Detail);
