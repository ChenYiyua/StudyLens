using Microsoft.AspNetCore.Mvc;
using StudyLens.Api.Contracts;
using StudyLens.Api.Data;
using StudyLens.Api.Models;
using StudyLens.Api.Services;

namespace StudyLens.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId}/history")]
public sealed class StudyHistoryController(
    IStudyAttemptRepository repository,
    CourseSearchService searchService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<StudyHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudyHistoryResponse>> GetHistory(
        string courseId,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!searchService.TryGetStatus(courseId, out _))
        {
            return NotFound();
        }

        if (limit is < 1 or > 100)
        {
            return BadRequest(new { error = "Limit must be between 1 and 100." });
        }

        var allAttempts = await repository.GetForCourseAsync(courseId, 5000, cancellationToken);
        var recentAttempts = allAttempts.Take(limit).Select(ToResponse).ToArray();
        var average = allAttempts.Count == 0
            ? (double?)null
            : Math.Round(allAttempts.Average(attempt => 100d * attempt.Score / attempt.MaxScore), 1);

        return Ok(new StudyHistoryResponse(
            courseId,
            allAttempts.Count,
            average,
            allAttempts.FirstOrDefault()?.CreatedAtUtc,
            recentAttempts));
    }

    [HttpDelete]
    [ProducesResponseType<DeleteStudyHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeleteStudyHistoryResponse>> DeleteHistory(
        string courseId,
        CancellationToken cancellationToken = default)
    {
        if (!searchService.TryGetStatus(courseId, out _))
        {
            return NotFound();
        }

        var deleted = await repository.DeleteForCourseAsync(courseId, cancellationToken);
        return Ok(new DeleteStudyHistoryResponse(courseId, deleted));
    }

    private static StudyAttemptResponse ToResponse(StudyAttempt attempt) => new(
        attempt.Id,
        attempt.CourseId,
        attempt.Question,
        attempt.StudentAnswer,
        attempt.Score,
        attempt.MaxScore,
        attempt.Summary,
        attempt.Strengths,
        attempt.MissingPoints,
        attempt.ImprovedAnswer,
        attempt.Model,
        attempt.Citations.Select(citation => new StudyAttemptCitationResponse(
            citation.Number,
            citation.ChunkId,
            citation.DocumentId,
            citation.Title,
            citation.RelativePath,
            citation.MaterialType,
            citation.Excerpt,
            citation.Page)).ToArray(),
        attempt.CreatedAtUtc);
}
