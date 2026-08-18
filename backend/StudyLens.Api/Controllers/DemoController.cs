using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using StudyLens.Api.Data;
using StudyLens.Api.Models;

namespace StudyLens.Api.Controllers;

[ApiController]
[Route("api/demo")]
public sealed class DemoController(ILearningEventRepository repository) : ControllerBase
{
    [HttpPost("seed")]
    public async Task<IActionResult> Seed(
        [FromQuery, Required, StringLength(64, MinimumLength = 8)] string participantId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var demoEvents = new[]
        {
            Create(participantId, AiProvider.ChatGpt, LearningActivity.Coding, now.AddDays(-4), 35, 7, 210, 4),
            Create(participantId, AiProvider.Claude, LearningActivity.UnderstandConcept, now.AddDays(-3), 24, 5, 120, 5),
            Create(participantId, AiProvider.Gemini, LearningActivity.Research, now.AddDays(-2), 42, 9, 280, 3),
            Create(participantId, AiProvider.ChatGpt, LearningActivity.Practice, now.AddDays(-1), 31, 6, 175, 4),
            Create(participantId, AiProvider.ChatGpt, LearningActivity.Coding, now, 48, 10, 340, 5)
        };

        foreach (var learningEvent in demoEvents)
        {
            await repository.AddAsync(learningEvent, cancellationToken);
        }

        return Ok(new { inserted = demoEvents.Length });
    }

    private static LearningEvent Create(
        string participantId,
        AiProvider provider,
        LearningActivity activity,
        DateTime startedAtUtc,
        int durationMinutes,
        int interactionCount,
        int promptWordCount,
        int helpfulnessRating) =>
        new()
        {
            ParticipantId = participantId.Trim(),
            Provider = provider,
            Activity = activity,
            StartedAtUtc = startedAtUtc,
            DurationMinutes = durationMinutes,
            InteractionCount = interactionCount,
            PromptWordCount = promptWordCount,
            HelpfulnessRating = helpfulnessRating
        };
}
