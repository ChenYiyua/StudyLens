using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using StudyLens.Api.Data;
using StudyLens.Api.Models;
using StudyLens.Api.Services;

namespace StudyLens.Api.Tests;

public sealed class CourseImportServiceTests
{
    [Fact]
    public async Task ImportAsync_RegistersCourseAndPersistsItForRestartDiscovery()
    {
        var root = CreateTestRoot();
        try
        {
            var environment = new TestWebHostEnvironment(root);
            var settings = new CourseImportOptions { ImportedCoursesRoot = Path.Combine(root, "imports") };
            var catalog = CourseCatalog.FromCorpora(
                "seed",
                CourseCorpus.FromIndex(CourseSearchServiceTests.CreateIndex("seed", "Seed")));
            var search = new CourseSearchService(catalog);
            var service = new CourseImportService(
                environment,
                Options.Create(settings),
                new FakeCourseIndexBuilder(),
                search);
            await using var content = new MemoryStream("Grounded evidence from a new course."u8.ToArray());
            IFormFile file = new FormFile(content, 0, content.Length, "files", "lecture.md");

            var status = await service.ImportAsync(
                "My New Course",
                [file],
                ["My Course/Lecture/lecture.md"],
                CancellationToken.None);

            Assert.True(status.Ready);
            Assert.Equal("My New Course", status.CourseName);
            Assert.Equal(1, status.DocumentCount);
            Assert.True(search.TryGetStatus(status.CourseId, out _));
            Assert.Single(search.Search(status.CourseId, "grounded evidence", 3, null).Results);
            Assert.True(File.Exists(Path.Combine(
                settings.ImportedCoursesRoot,
                status.CourseId,
                "source",
                "My Course",
                "Lecture",
                "lecture.md")));

            var reloadedCatalog = new CourseCatalog(
                environment,
                Options.Create(new CourseCatalogOptions
                {
                    DefaultCourseId = status.CourseId,
                    ImportedCoursesRoot = settings.ImportedCoursesRoot,
                }));
            Assert.True(reloadedCatalog.TryGetCourse(status.CourseId, out var reloaded));
            Assert.True(reloaded.Ready);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_RejectsUnsupportedFilesBeforeIndexing()
    {
        var root = CreateTestRoot();
        try
        {
            var builder = new FakeCourseIndexBuilder();
            var catalog = CourseCatalog.FromCorpora(
                "seed",
                CourseCorpus.FromIndex(CourseSearchServiceTests.CreateIndex("seed", "Seed")));
            var service = new CourseImportService(
                new TestWebHostEnvironment(root),
                Options.Create(new CourseImportOptions { ImportedCoursesRoot = Path.Combine(root, "imports") }),
                builder,
                new CourseSearchService(catalog));
            await using var content = new MemoryStream([1, 2, 3]);
            IFormFile file = new FormFile(content, 0, content.Length, "files", "malware.exe");

            var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.ImportAsync("Unsafe Course", [file], null, CancellationToken.None));

            Assert.Contains("Unsupported file", exception.Message);
            Assert.Equal(0, builder.CallCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_SkipsUnsupportedFilesAndImportsTheSupportedRemainder()
    {
        var root = CreateTestRoot();
        try
        {
            var builder = new FakeCourseIndexBuilder();
            var catalog = CourseCatalog.FromCorpora(
                "seed",
                CourseCorpus.FromIndex(CourseSearchServiceTests.CreateIndex("seed", "Seed")));
            var service = new CourseImportService(
                new TestWebHostEnvironment(root),
                Options.Create(new CourseImportOptions { ImportedCoursesRoot = Path.Combine(root, "imports") }),
                builder,
                new CourseSearchService(catalog));
            await using var lectureContent = new MemoryStream("Grounded lecture evidence."u8.ToArray());
            await using var imageContent = new MemoryStream([1, 2, 3]);
            IFormFile lecture = new FormFile(
                lectureContent,
                0,
                lectureContent.Length,
                "files",
                "lecture.md");
            IFormFile image = new FormFile(
                imageContent,
                0,
                imageContent.Length,
                "files",
                "cover.png");

            var status = await service.ImportAsync(
                "Mixed Course",
                [lecture, image],
                ["Mixed Course/Lecture/lecture.md", "Mixed Course/cover.png"],
                CancellationToken.None);

            Assert.True(status.Ready);
            Assert.Equal(1, status.DocumentCount);
            Assert.Equal(1, builder.CallCount);
            Assert.True(File.Exists(Path.Combine(
                root,
                "imports",
                status.CourseId,
                "source",
                "Mixed Course",
                "Lecture",
                "lecture.md")));
            Assert.False(File.Exists(Path.Combine(
                root,
                "imports",
                status.CourseId,
                "source",
                "Mixed Course",
                "cover.png")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImportAsync_RejectsFolderTraversalBeforeWritingFiles()
    {
        var root = CreateTestRoot();
        try
        {
            var builder = new FakeCourseIndexBuilder();
            var catalog = CourseCatalog.FromCorpora(
                "seed",
                CourseCorpus.FromIndex(CourseSearchServiceTests.CreateIndex("seed", "Seed")));
            var service = new CourseImportService(
                new TestWebHostEnvironment(root),
                Options.Create(new CourseImportOptions { ImportedCoursesRoot = Path.Combine(root, "imports") }),
                builder,
                new CourseSearchService(catalog));
            await using var content = new MemoryStream("safe content"u8.ToArray());
            IFormFile file = new FormFile(content, 0, content.Length, "files", "lecture.md");

            await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(
                "Unsafe Folder",
                [file],
                ["../../lecture.md"],
                CancellationToken.None));

            Assert.Equal(0, builder.CallCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"studylens-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeCourseIndexBuilder : ICourseIndexBuilder
    {
        public int CallCount { get; private set; }

        public Task BuildAsync(
            string sourcePath,
            string outputPath,
            string courseId,
            string courseName,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var index = new CourseIndex(
                1,
                new CourseInfo(courseId, courseName),
                DateTimeOffset.UtcNow,
                new CourseStatistics(1, 1, 0, 1),
                [new CourseDocument("document", "lecture", "lecture.md", "lecture", 1, 1)],
                [new CourseChunk(
                    "chunk",
                    "document",
                    "lecture",
                    "lecture.md",
                    "lecture",
                    1,
                    "Grounded evidence from a new course.")]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(
                outputPath,
                JsonSerializer.Serialize(index, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            return Task.CompletedTask;
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "StudyLens.Api.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = "Testing";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
