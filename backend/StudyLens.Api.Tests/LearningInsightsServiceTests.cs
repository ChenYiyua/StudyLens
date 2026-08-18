using StudyLens.Api.Data;
using StudyLens.Api.Models;
using StudyLens.Api.Services;

namespace StudyLens.Api.Tests;

public sealed class LearningInsightsServiceTests
{
    [Fact]
    public async Task GetAsync_AggregatesOnlyRequestedParticipant()
    {
        var repository = new InMemoryLearningEventRepository();
        await repository.AddAsync(CreateEvent("student-a", AiProvider.ChatGpt, 20, 5));
        await repository.AddAsync(CreateEvent("student-a", AiProvider.Claude, 40, 3));
        await repository.AddAsync(CreateEvent("student-b", AiProvider.Gemini, 99, 1));

        var result = await new LearningInsightsService(repository).GetAsync("student-a");

        Assert.Equal(2, result.TotalSessions);
        Assert.Equal(60, result.TotalMinutes);
        Assert.Equal(4, result.AverageHelpfulness);
        Assert.Equal(2, result.ByProvider.Count);
    }

    [Fact]
    public async Task GetAsync_ReturnsZerosWhenNoEventsExist()
    {
        var result = await new LearningInsightsService(new InMemoryLearningEventRepository())
            .GetAsync("student-empty");

        Assert.Equal(0, result.TotalSessions);
        Assert.Equal(0, result.TotalMinutes);
        Assert.Equal(0, result.AverageHelpfulness);
        Assert.Empty(result.DailyUsage);
    }

    private static LearningEvent CreateEvent(
        string participantId,
        AiProvider provider,
        int durationMinutes,
        int helpfulnessRating) =>
        new()
        {
            ParticipantId = participantId,
            Provider = provider,
            Activity = LearningActivity.Coding,
            StartedAtUtc = DateTime.UtcNow,
            DurationMinutes = durationMinutes,
            InteractionCount = 2,
            PromptWordCount = 50,
            HelpfulnessRating = helpfulnessRating
        };
}
