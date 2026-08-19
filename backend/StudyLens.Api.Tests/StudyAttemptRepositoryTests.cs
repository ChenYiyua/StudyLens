using StudyLens.Api.Data;
using StudyLens.Api.Models;

namespace StudyLens.Api.Tests;

public sealed class StudyAttemptRepositoryTests
{
    [Fact]
    public async Task LocalJsonRepository_PersistsFiltersAndReloadsAttempts()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var path = Path.Combine(testRoot, "attempts.json");
            var repository = new LocalJsonStudyAttemptRepository(path);
            await repository.AddAsync(CreateAttempt("older", "course-a", DateTimeOffset.Parse("2026-08-18T10:00:00Z")));
            await repository.AddAsync(CreateAttempt("other", "course-b", DateTimeOffset.Parse("2026-08-18T11:00:00Z")));
            await repository.AddAsync(CreateAttempt("newer", "course-a", DateTimeOffset.Parse("2026-08-18T12:00:00Z")));

            var reloaded = new LocalJsonStudyAttemptRepository(path);
            var attempts = await reloaded.GetForCourseAsync("course-a", 20);

            Assert.Equal(["newer", "older"], attempts.Select(attempt => attempt.Id));
            Assert.DoesNotContain(testRoot, await File.ReadAllTextAsync(path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LocalJsonRepository_DeletesOnlyRequestedCourse()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var repository = new LocalJsonStudyAttemptRepository(Path.Combine(testRoot, "attempts.json"));
            await repository.AddAsync(CreateAttempt("one", "course-a", DateTimeOffset.UtcNow));
            await repository.AddAsync(CreateAttempt("two", "course-b", DateTimeOffset.UtcNow));

            var deleted = await repository.DeleteForCourseAsync("course-a");

            Assert.Equal(1, deleted);
            Assert.Empty(await repository.GetForCourseAsync("course-a", 20));
            Assert.Single(await repository.GetForCourseAsync("course-b", 20));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static StudyAttempt CreateAttempt(string id, string courseId, DateTimeOffset createdAtUtc) => new(
        id,
        courseId,
        "Define source grounding.",
        "It links an answer to evidence.",
        8,
        10,
        "Good answer.",
        ["Correct evidence link"],
        ["Add limitations"],
        "Source grounding links generated claims to auditable evidence.",
        "test-model",
        [new StudyAttemptCitation(
            1,
            "chunk",
            "doc",
            "Grounding",
            "grounding.md",
            "lecture",
            "Grounded tutoring retains citations.",
            1)],
        createdAtUtc);

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"studylens-attempts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
