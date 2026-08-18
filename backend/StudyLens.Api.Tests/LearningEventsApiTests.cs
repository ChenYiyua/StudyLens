using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using StudyLens.Api.Contracts;

namespace StudyLens.Api.Tests;

public sealed class LearningEventsApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
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

    [Fact]
    public async Task Export_ReturnsOnlyRequestedParticipantsData()
    {
        var participantId = $"export-{Guid.NewGuid():N}";
        await CreateEventAsync(participantId);
        await CreateEventAsync($"other-{Guid.NewGuid():N}");

        var export = await _client.GetFromJsonAsync<LearningDataExport>(
            $"/api/events/export?participantId={participantId}",
            JsonOptions);

        Assert.NotNull(export);
        Assert.Equal(1, export.SchemaVersion);
        Assert.Equal(participantId, export.ParticipantId);
        Assert.Single(export.Events);
        Assert.All(export.Events, item => Assert.Equal(participantId, item.ParticipantId));
    }

    [Fact]
    public async Task Delete_RemovesOnlyRequestedParticipantsData()
    {
        var participantId = $"delete-{Guid.NewGuid():N}";
        var otherParticipantId = $"keep-{Guid.NewGuid():N}";
        await CreateEventAsync(participantId);
        await CreateEventAsync(participantId);
        await CreateEventAsync(otherParticipantId);

        var deleteResponse = await _client.DeleteAsync(
            $"/api/events?participantId={participantId}");
        var deletedParticipantEvents = await _client.GetFromJsonAsync<LearningEventResponse[]>(
            $"/api/events?participantId={participantId}",
            JsonOptions);
        var otherParticipantEvents = await _client.GetFromJsonAsync<LearningEventResponse[]>(
            $"/api/events?participantId={otherParticipantId}",
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.NotNull(deletedParticipantEvents);
        Assert.Empty(deletedParticipantEvents);
        Assert.NotNull(otherParticipantEvents);
        Assert.Single(otherParticipantEvents);
    }

    [Theory]
    [InlineData("/api/events?participantId=short")]
    [InlineData("/api/events/export?participantId=short")]
    public async Task ParticipantScopedReads_WithInvalidIdentifier_ReturnBadRequest(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task CreateEventAsync(string participantId)
    {
        var response = await _client.PostAsJsonAsync("/api/events", new
        {
            participantId,
            provider = "ChatGpt",
            activity = "Coding",
            startedAtUtc = DateTime.UtcNow,
            durationMinutes = 25,
            interactionCount = 4,
            promptWordCount = 80,
            helpfulnessRating = 5
        });

        response.EnsureSuccessStatusCode();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
