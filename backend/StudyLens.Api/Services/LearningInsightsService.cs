using StudyLens.Api.Contracts;
using StudyLens.Api.Data;

namespace StudyLens.Api.Services;

public sealed class LearningInsightsService(ILearningEventRepository repository)
{
    public async Task<InsightsResponse> GetAsync(
        string participantId,
        CancellationToken cancellationToken = default)
    {
        var events = await repository.GetForParticipantAsync(participantId, cancellationToken);

        var averageHelpfulness = events.Count == 0
            ? 0
            : Math.Round(events.Average(item => item.HelpfulnessRating), 1);

        var byProvider = events
            .GroupBy(item => item.Provider)
            .OrderByDescending(group => group.Count())
            .Select(group => new DimensionCount(group.Key.ToString(), group.Count()))
            .ToArray();

        var byActivity = events
            .GroupBy(item => item.Activity)
            .OrderByDescending(group => group.Count())
            .Select(group => new DimensionCount(group.Key.ToString(), group.Count()))
            .ToArray();

        var dailyUsage = events
            .GroupBy(item => DateOnly.FromDateTime(item.StartedAtUtc))
            .OrderBy(group => group.Key)
            .Select(group => new DailyUsage(
                group.Key,
                group.Count(),
                group.Sum(item => item.DurationMinutes)))
            .ToArray();

        return new InsightsResponse(
            events.Count,
            events.Sum(item => item.DurationMinutes),
            averageHelpfulness,
            byProvider,
            byActivity,
            dailyUsage);
    }
}
