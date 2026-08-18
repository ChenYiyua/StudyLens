using System.ComponentModel.DataAnnotations;
using StudyLens.Api.Models;

namespace StudyLens.Api.Contracts;

public sealed class CreateLearningEventRequest
{
    [Required]
    [StringLength(64, MinimumLength = 8)]
    public required string ParticipantId { get; init; }

    public required AiProvider Provider { get; init; }
    public required LearningActivity Activity { get; init; }
    public DateTime? StartedAtUtc { get; init; }

    [Range(1, 480)]
    public required int DurationMinutes { get; init; }

    [Range(1, 100)]
    public required int InteractionCount { get; init; }

    [Range(0, 5000)]
    public required int PromptWordCount { get; init; }

    [Range(1, 5)]
    public required int HelpfulnessRating { get; init; }
}
