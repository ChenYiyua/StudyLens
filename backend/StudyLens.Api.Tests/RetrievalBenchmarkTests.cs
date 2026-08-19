using System.Text.Json;
using StudyLens.Api.Data;
using StudyLens.Api.Models;
using StudyLens.Api.Services;

namespace StudyLens.Api.Tests;

public sealed class RetrievalBenchmarkTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void DemoBenchmark_RetrievesExpectedDocumentFirst()
    {
        var index = ReadJson<CourseIndex>("samples", "demo-course-index.json");
        var cases = ReadJson<IReadOnlyList<RetrievalCase>>("evals", "demo-retrieval-cases.json");
        var service = new CourseSearchService(CourseCatalog.FromCorpora(
            index.Course.Id,
            CourseCorpus.FromIndex(index)));

        foreach (var benchmark in cases)
        {
            var result = service.Search(index.Course.Id, benchmark.Query, 3, null);

            Assert.NotEmpty(result.Results);
            Assert.Equal(benchmark.ExpectedTopPath, result.Results[0].RelativePath);
        }
    }

    private static T ReadJson<T>(params string[] relativePath)
    {
        var path = Path.Combine([AppContext.BaseDirectory, .. relativePath]);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException($"Could not read benchmark fixture: {path}");
    }

    private sealed record RetrievalCase(string Query, string ExpectedTopPath);
}
