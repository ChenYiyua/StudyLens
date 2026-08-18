using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using StudyLens.Api.Contracts;
using StudyLens.Api.Data;
using StudyLens.Api.Models;

namespace StudyLens.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class LearningEventsController(ILearningEventRepository repository) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<LearningEventResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LearningEventResponse>> Create(
        CreateLearningEventRequest request,
        CancellationToken cancellationToken)
    {
        var learningEvent = new LearningEvent
        {
            ParticipantId = request.ParticipantId.Trim(),
            Provider = request.Provider,
            Activity = request.Activity,
            StartedAtUtc = (request.StartedAtUtc ?? DateTime.UtcNow).ToUniversalTime(),
            DurationMinutes = request.DurationMinutes,
            InteractionCount = request.InteractionCount,
            PromptWordCount = request.PromptWordCount,
            HelpfulnessRating = request.HelpfulnessRating
        };

        await repository.AddAsync(learningEvent, cancellationToken);
        var response = ToResponse(learningEvent);

        return CreatedAtAction(nameof(GetById), new { id = learningEvent.Id }, response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType<LearningEventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LearningEventResponse>> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        var learningEvent = await repository.GetByIdAsync(id, cancellationToken);
        return learningEvent is null ? NotFound() : Ok(ToResponse(learningEvent));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LearningEventResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearningEventResponse>>> GetForParticipant(
        [FromQuery, Required, StringLength(64, MinimumLength = 8)] string participantId,
        CancellationToken cancellationToken)
    {
        var events = await repository.GetForParticipantAsync(participantId.Trim(), cancellationToken);
        return Ok(events.Select(ToResponse).ToArray());
    }

    [HttpGet("export")]
    [ProducesResponseType<LearningDataExport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LearningDataExport>> Export(
        [FromQuery, Required, StringLength(64, MinimumLength = 8)] string participantId,
        CancellationToken cancellationToken)
    {
        var normalizedParticipantId = participantId.Trim();
        var events = await repository.GetForParticipantAsync(normalizedParticipantId, cancellationToken);
        var export = new LearningDataExport(
            SchemaVersion: 1,
            ExportedAtUtc: DateTime.UtcNow,
            ParticipantId: normalizedParticipantId,
            Events: events.Select(ToResponse).ToArray());

        return Ok(export);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteForParticipant(
        [FromQuery, Required, StringLength(64, MinimumLength = 8)] string participantId,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteForParticipantAsync(
            participantId.Trim(),
            cancellationToken);

        return Ok(new { deleted });
    }

    private static LearningEventResponse ToResponse(LearningEvent item) =>
        new(
            item.Id,
            item.ParticipantId,
            item.Provider,
            item.Activity,
            item.StartedAtUtc,
            item.DurationMinutes,
            item.InteractionCount,
            item.PromptWordCount,
            item.HelpfulnessRating);
}
