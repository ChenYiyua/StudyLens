using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StudyLens.Api.Contracts;
using StudyLens.Api.Data;

namespace StudyLens.Api.Tests;

public sealed class CourseApiTests : IClassFixture<CourseApiTests.CourseApiFactory>
{
    private readonly HttpClient client;

    public CourseApiTests(CourseApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStatus_ReturnsReadyCorpusMetadata()
    {
        var response = await client.GetFromJsonAsync<CourseStatusResponse>("/api/courses/test-course");

        Assert.NotNull(response);
        Assert.True(response.Ready);
        Assert.Equal("Test EAM Course", response.CourseName);
        Assert.Equal(30, response.PageCount);
    }

    [Fact]
    public async Task GetCourses_ReturnsDefaultCourseAndCatalog()
    {
        var response = await client.GetFromJsonAsync<CourseCatalogResponse>("/api/courses");

        Assert.NotNull(response);
        Assert.Equal("test-course", response.DefaultCourseId);
        var course = Assert.Single(response.Courses);
        Assert.Equal("test-course", course.CourseId);
        Assert.True(course.Ready);
    }

    [Fact]
    public async Task GetLearningPath_ReturnsLectureMaterials()
    {
        var response = await client.GetFromJsonAsync<CourseLearningPathResponse>(
            "/api/courses/test-course/learning-path");

        Assert.NotNull(response);
        var lecture = Assert.Single(response.Lectures);
        Assert.Equal("lecture-4", lecture.DocumentId);
    }

    [Fact]
    public async Task Search_ReturnsPageCitedEvidence()
    {
        var response = await client.GetFromJsonAsync<CourseSearchResponse>(
            "/api/courses/test-course/search?query=architecture%20view%20viewpoint&limit=2");

        Assert.NotNull(response);
        Assert.NotEmpty(response.Results);
        Assert.All(response.Results, result =>
        {
            Assert.False(string.IsNullOrWhiteSpace(result.RelativePath));
            Assert.True(result.Page > 0);
        });
    }

    [Fact]
    public async Task GetChunk_ReturnsCompleteIndexedPassageAndPageMetadata()
    {
        var response = await client.GetFromJsonAsync<CourseChunkDetailResponse>(
            "/api/courses/test-course/chunks/chunk-lecture");

        Assert.NotNull(response);
        Assert.Equal("chunk-lecture", response.ChunkId);
        Assert.Equal("Lecture/L04 Foundations.pdf", response.RelativePath);
        Assert.Equal(20, response.Page);
        Assert.Contains("constructing and interpreting", response.Text);
    }

    [Fact]
    public async Task GetChunk_WithUnknownId_ReturnsNotFound()
    {
        var response = await client.GetAsync("/api/courses/test-course/chunks/not-a-chunk");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/courses/test-course/search?query=x")]
    [InlineData("/api/courses/test-course/search?query=architecture&limit=11")]
    public async Task Search_WithInvalidInput_ReturnsBadRequest(string path)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public sealed class CourseApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(CourseCatalog.FromCorpora(
                    "test-course",
                    CourseCorpus.FromIndex(CourseSearchServiceTests.CreateIndex())));
            });
        }
    }
}
