using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StudyLens.Api.Contracts;
using StudyLens.Api.Data;
using StudyLens.Api.Models;
using StudyLens.Api.Services;

namespace StudyLens.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class TutorController(
    TutorService tutorService,
    CourseSearchService searchService,
    IStudyAttemptRepository attemptRepository) : ControllerBase
{
    [HttpGet("ai/status")]
    [ProducesResponseType<AiProviderCatalog>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiProviderCatalog>> GetAiStatus(CancellationToken cancellationToken) =>
        Ok(await tutorService.GetProviderCatalogAsync(cancellationToken));

    [HttpPost("courses/{courseId}/tutor/explain")]
    [ProducesResponseType<ExplainResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExplainResponse>> Explain(
        string courseId,
        ExplainRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateCourseAndText(courseId, request.Question, "Question");
        if (validation is not null)
        {
            return validation;
        }

        return await Execute(() => tutorService.ExplainAsync(courseId, request, cancellationToken));
    }

    [HttpPost("courses/{courseId}/tutor/lecture")]
    [ProducesResponseType<ExplainResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExplainResponse>> ExplainLecture(
        string courseId,
        LectureExplainRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateCourseAndText(courseId, request.DocumentId, "DocumentId");
        if (validation is not null)
        {
            return validation;
        }

        return await Execute(() => tutorService.ExplainLectureAsync(courseId, request, cancellationToken));
    }

    [HttpPost("courses/{courseId}/tutor/exercise")]
    [ProducesResponseType<ExplainResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExplainResponse>> ExplainExercise(
        string courseId,
        ExerciseExplainRequest request,
        CancellationToken cancellationToken)
    {
        var selectedDocumentId = request.ExerciseDocumentId ?? request.SolutionDocumentId ?? string.Empty;
        var validation = ValidateCourseAndText(courseId, selectedDocumentId, "DocumentId");
        if (validation is not null)
        {
            return validation;
        }

        return await Execute(() =>
            tutorService.ExplainExerciseAsync(courseId, request, cancellationToken));
    }

    [HttpPost("courses/{courseId}/tutor/practice")]
    [ProducesResponseType<PracticeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PracticeResponse>> Practice(
        string courseId,
        PracticeRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateCourseAndText(courseId, request.Topic, "Topic");
        if (validation is not null)
        {
            return validation;
        }

        if (request.QuestionCount is < 1 or > 5)
        {
            return BadRequest(new { error = "QuestionCount must be between 1 and 5." });
        }

        return await Execute(() => tutorService.CreatePracticeAsync(courseId, request, cancellationToken));
    }

    [HttpPost("courses/{courseId}/tutor/grade")]
    [ProducesResponseType<GradeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GradeResponse>> Grade(
        string courseId,
        GradeRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateCourseAndText(courseId, request.Question, "Question");
        if (validation is not null)
        {
            return validation;
        }

        if (string.IsNullOrWhiteSpace(request.StudentAnswer) || request.StudentAnswer.Length > 6000)
        {
            return BadRequest(new { error = "StudentAnswer must contain between 1 and 6000 characters." });
        }

        return await Execute(async () =>
        {
            var grade = await tutorService.GradeAsync(courseId, request, cancellationToken);
            var attemptId = Guid.NewGuid().ToString("N");
            var createdAtUtc = DateTimeOffset.UtcNow;
            var persistedGrade = grade with { AttemptId = attemptId, CreatedAtUtc = createdAtUtc };
            await attemptRepository.AddAsync(
                new StudyAttempt(
                    attemptId,
                    courseId,
                    request.Question.Trim(),
                    request.StudentAnswer.Trim(),
                    grade.Score,
                    grade.MaxScore,
                    grade.Summary,
                    grade.Strengths.ToArray(),
                    grade.MissingPoints.ToArray(),
                    grade.ImprovedAnswer,
                    grade.Model,
                    grade.Citations.Select(citation => new StudyAttemptCitation(
                        citation.Number,
                        citation.ChunkId,
                        citation.DocumentId,
                        citation.Title,
                        citation.RelativePath,
                        citation.MaterialType,
                        citation.Excerpt,
                        citation.Page)).ToArray(),
                    createdAtUtc),
                cancellationToken);
            return persistedGrade;
        });
    }

    private ActionResult? ValidateCourseAndText(string courseId, string value, string fieldName)
    {
        if (!searchService.TryGetStatus(courseId, out _))
        {
            return NotFound();
        }

        if (!searchService.IsReady(courseId))
        {
            return Problem(
                searchService.GetError(courseId),
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Course corpus is not ready");
        }

        return string.IsNullOrWhiteSpace(value) || value.Length > 600
            ? BadRequest(new { error = $"{fieldName} must contain between 1 and 600 characters." })
            : null;
    }

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> operation)
    {
        try
        {
            return Ok(await operation());
        }
        catch (HttpRequestException exception)
        {
            return Problem(
                exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Local AI model is unavailable");
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                exception.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Tutor could not complete the request");
        }
        catch (JsonException exception)
        {
            return Problem(
                exception.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Tutor returned an invalid structured response");
        }
    }
}
