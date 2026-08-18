using StudyLens.Api.Models;

namespace StudyLens.Api.Data;

public interface ILearningEventRepository
{
    Task AddAsync(LearningEvent learningEvent, CancellationToken cancellationToken = default);
    Task<LearningEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LearningEvent>> GetForParticipantAsync(
        string participantId,
        CancellationToken cancellationToken = default);
}
