using System.Collections.Concurrent;
using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public sealed class InMemoryLearningEventRepository : ILearningEventRepository
{
    private readonly ConcurrentDictionary<string, LearningEvent> _events = new();

    public Task AddAsync(LearningEvent learningEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _events[learningEvent.Id] = learningEvent;
        return Task.CompletedTask;
    }

    public Task<LearningEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _events.TryGetValue(id, out var learningEvent);
        return Task.FromResult(learningEvent);
    }

    public Task<IReadOnlyList<LearningEvent>> GetForParticipantAsync(
        string participantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<LearningEvent> result = _events.Values
            .Where(item => item.ParticipantId == participantId)
            .OrderByDescending(item => item.StartedAtUtc)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<long> DeleteForParticipantAsync(
        string participantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var matchingIds = _events
            .Where(pair => pair.Value.ParticipantId == participantId)
            .Select(pair => pair.Key)
            .ToArray();

        long deleted = 0;
        foreach (var id in matchingIds)
        {
            if (_events.TryRemove(id, out _))
            {
                deleted++;
            }
        }

        return Task.FromResult(deleted);
    }
}
