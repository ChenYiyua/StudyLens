using StudyLens.Api.Data;
using StudyLens.Api.Models;
using StudyLens.Api.Services;

namespace StudyLens.Api.Tests;

public sealed class CourseSearchServiceTests
{
    [Fact]
    public void Search_RanksExactConceptAndPreservesCitation()
    {
        var service = CreateService();

        var response = service.Search("test-course", "architecture view viewpoint", 3, null);

        Assert.Equal(2, response.ResultCount);
        Assert.Equal("L04 Foundations", response.Results[0].Title);
        Assert.Equal("Lecture/L04 Foundations.pdf", response.Results[0].RelativePath);
        Assert.Equal("lecture", response.Results[0].MaterialType);
        Assert.Equal(20, response.Results[0].Page);
        Assert.Contains("viewpoint", response.Results[0].Excerpt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Search_WithMaterialFilter_ReturnsOnlyThatType()
    {
        var service = CreateService();

        var response = service.Search("test-course", "architecture view", 5, "exam");

        var result = Assert.Single(response.Results);
        Assert.Equal("exam", result.MaterialType);
    }

    [Fact]
    public void Status_ReportsCorpusCounts()
    {
        var status = Assert.Single(CreateService().GetStatuses());

        Assert.True(status.Ready);
        Assert.Equal(2, status.DocumentCount);
        Assert.Equal(2, status.ChunkCount);
        Assert.Equal(1, status.MaterialTypes["lecture"]);
    }

    [Fact]
    public void Catalog_ListsMultipleCoursesAndKeepsThemIsolated()
    {
        var secondIndex = CreateIndex("second-course", "Second Course");
        var service = new CourseSearchService(CourseCatalog.FromCorpora(
            "test-course",
            CourseCorpus.FromIndex(CreateIndex()),
            CourseCorpus.FromIndex(secondIndex)));

        var statuses = service.GetStatuses();

        Assert.Equal(2, statuses.Count);
        Assert.Equal("test-course", service.DefaultCourseId);
        Assert.Equal("second-course", service.Search(
            "second-course", "architecture viewpoint", 2, null).CourseId);
    }

    [Fact]
    public void LearningPath_PairsCourseExerciseWithProvidedSolution()
    {
        var service = new CourseSearchService(CourseCatalog.FromCorpora(
            "test-course",
            CourseCorpus.FromIndex(CreateLearningIndex())));

        var path = service.GetLearningPath("test-course");

        Assert.Single(path.Lectures);
        var unit = Assert.Single(path.Exercises);
        Assert.Equal("H02", unit.Exercise?.Title);
        Assert.Equal("H02_Solution", unit.Solution?.Title);
    }

    [Fact]
    public void DocumentEvidence_IsScopedToSelectedDocument()
    {
        var service = new CourseSearchService(CourseCatalog.FromCorpora(
            "test-course",
            CourseCorpus.FromIndex(CreateLearningIndex())));

        var evidence = service.GetDocumentEvidence("test-course", "exercise-2", 8);

        var result = Assert.Single(evidence.Results);
        Assert.Equal("exercise-2", result.DocumentId);
        Assert.Equal("exercise", result.MaterialType);
    }

    private static CourseSearchService CreateService() => new(CourseCatalog.FromCorpora(
        "test-course",
        CourseCorpus.FromIndex(CreateIndex())));

    internal static CourseIndex CreateIndex(
        string courseId = "test-course",
        string courseName = "Test EAM Course") => new(
        1,
        new CourseInfo(courseId, courseName),
        DateTimeOffset.Parse("2026-08-18T12:00:00Z"),
        new CourseStatistics(2, 30, 0, 2),
        [
            new CourseDocument("lecture-4", "L04 Foundations", "Lecture/L04 Foundations.pdf", "lecture", 25, 1),
            new CourseDocument("mock", "Mock Exam", "MockExam.pdf", "exam", 5, 1),
        ],
        [
            new CourseChunk(
                "chunk-lecture", "lecture-4", "L04 Foundations", "Lecture/L04 Foundations.pdf",
                "lecture", 20,
                "An architecture view represents a system from the perspective of concerns. " +
                "An architecture viewpoint defines conventions for constructing and interpreting that view."),
            new CourseChunk(
                "chunk-exam", "mock", "Mock Exam", "MockExam.pdf", "exam", 3,
                "Explain the distinction between an architecture view and its governing viewpoint."),
        ]);

    internal static CourseIndex CreateLearningIndex()
    {
        var index = CreateIndex();
        return index with
        {
            Statistics = new CourseStatistics(4, 34, 0, 4),
            Documents = index.Documents.Concat([
                new CourseDocument("exercise-2", "H02", "Exercise/H02.pdf", "exercise", 2, 1),
                new CourseDocument("solution-2", "H02_Solution", "Solution/H02_Solution.pdf", "solution", 2, 1),
            ]).ToArray(),
            Chunks = index.Chunks.Concat([
                new CourseChunk(
                    "chunk-exercise", "exercise-2", "H02", "Exercise/H02.pdf", "exercise", 1,
                    "The exercise asks students to map stakeholders to architecture concerns."),
                new CourseChunk(
                    "chunk-solution", "solution-2", "H02_Solution", "Solution/H02_Solution.pdf", "solution", 1,
                    "The solution identifies the stakeholder, concern, viewpoint, and resulting view."),
            ]).ToArray(),
        };
    }
}
