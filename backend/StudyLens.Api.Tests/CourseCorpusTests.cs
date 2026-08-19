using StudyLens.Api.Data;
using StudyLens.Api.Models;

namespace StudyLens.Api.Tests;

public sealed class CourseCorpusTests
{
    [Fact]
    public void TryResolveDocument_ReturnsConfiguredPdfInsideCourseFolder()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var lectureFolder = Directory.CreateDirectory(Path.Combine(testRoot, "Lecture"));
            var pdfPath = Path.Combine(lectureFolder.FullName, "L04 Foundations.pdf");
            File.WriteAllBytes(pdfPath, "%PDF-test"u8.ToArray());
            var corpus = CourseCorpus.FromIndex(CourseSearchServiceTests.CreateIndex(), testRoot);

            var found = corpus.TryResolveDocument("lecture-4", out var resolvedPath);

            Assert.True(found);
            Assert.Equal(Path.GetFullPath(pdfPath), resolvedPath);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void TryResolveDocument_RejectsPathOutsideConfiguredCourseFolder()
    {
        var sandbox = CreateTestRoot();
        try
        {
            var courseRoot = Directory.CreateDirectory(Path.Combine(sandbox, "course")).FullName;
            File.WriteAllBytes(Path.Combine(sandbox, "outside.pdf"), "%PDF-test"u8.ToArray());
            var maliciousIndex = new CourseIndex(
                1,
                new CourseInfo("test", "Test"),
                DateTimeOffset.UtcNow,
                new CourseStatistics(1, 1, 0, 0),
                [new CourseDocument("escape", "Outside", "../outside.pdf", "lecture", 1, 0)],
                []);
            var corpus = CourseCorpus.FromIndex(maliciousIndex, courseRoot);

            Assert.False(corpus.TryResolveDocument("escape", out _));
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public void TryResolveDocument_AllowsMarkdownInsideCourseFolder()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var markdownPath = Path.Combine(testRoot, "grounded-tutoring.md");
            File.WriteAllText(markdownPath, "Grounded tutoring retains citations.");
            var index = new CourseIndex(
                1,
                new CourseInfo("demo", "Demo"),
                DateTimeOffset.UtcNow,
                new CourseStatistics(1, 1, 0, 1),
                [new CourseDocument("notes", "Notes", "grounded-tutoring.md", "lecture", 1, 1)],
                []);
            var corpus = CourseCorpus.FromIndex(index, testRoot);

            Assert.True(corpus.TryResolveDocument("notes", out var resolvedPath));
            Assert.Equal(Path.GetFullPath(markdownPath), resolvedPath);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"studylens-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
