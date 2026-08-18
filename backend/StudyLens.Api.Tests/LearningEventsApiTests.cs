using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StudyLens.Api.Tests;

public sealed class LearningEventsApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_WithValidMetadata_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/events", new
        {
            participantId = "test-student",
            provider = "ChatGpt",
            activity = "Coding",
            startedAtUtc = DateTime.UtcNow,
            durationMinutes = 25,
            interactionCount = 4,
            promptWordCount = 80,
            helpfulnessRating = 5
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithRawPromptField_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/events", new
        {
            participantId = "test-student",
            provider = "ChatGpt",
            activity = "Coding",
            durationMinutes = 25,
            interactionCount = 4,
            promptWordCount = 80,
            helpfulnessRating = 5,
            rawPrompt = "This field must never be accepted."
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithOutOfRangeRating_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/events", new
        {
            participantId = "test-student",
            provider = "Claude",
            activity = "Research",
            durationMinutes = 25,
            interactionCount = 4,
            promptWordCount = 80,
            helpfulnessRating = 8
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
