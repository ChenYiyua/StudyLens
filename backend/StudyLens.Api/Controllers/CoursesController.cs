using Microsoft.AspNetCore.Mvc;
using StudyLens.Api.Contracts;
using StudyLens.Api.Data;
using StudyLens.Api.Services;

namespace StudyLens.Api.Controllers;

[ApiController]
[Route("api/courses")]
public sealed class CoursesController(
    CourseSearchService searchService,
    CourseCatalog catalog,
    CoursePagePreviewService pagePreviewService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CourseCatalogResponse>(StatusCodes.Status200OK)]
    public ActionResult<CourseCatalogResponse> GetCourses() =>
        Ok(new CourseCatalogResponse(searchService.DefaultCourseId, searchService.GetStatuses()));

    [HttpGet("{courseId}")]
    [ProducesResponseType<CourseStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CourseStatusResponse> GetStatus(string courseId) =>
        searchService.TryGetStatus(courseId, out var status) ? Ok(status) : NotFound();

    [HttpGet("{courseId}/learning-path")]
    [ProducesResponseType<CourseLearningPathResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CourseLearningPathResponse> GetLearningPath(string courseId)
    {
        if (!searchService.IsReady(courseId))
        {
            return NotFound();
        }

        return Ok(searchService.GetLearningPath(courseId));
    }

    [HttpGet("{courseId}/search")]
    [ProducesResponseType<CourseSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<CourseSearchResponse> Search(
        string courseId,
        [FromQuery] string query,
        [FromQuery] int limit = 6,
        [FromQuery] string? materialType = null)
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

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2 || query.Length > 300)
        {
            return BadRequest(new { error = "Query must contain between 2 and 300 characters." });
        }

        if (limit is < 1 or > 10)
        {
            return BadRequest(new { error = "Limit must be between 1 and 10." });
        }

        return Ok(searchService.Search(courseId, query.Trim(), limit, NormalizeFilter(materialType)));
    }

    [HttpGet("{courseId}/chunks/{chunkId}")]
    [ProducesResponseType<CourseChunkDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<CourseChunkDetailResponse> GetChunk(string courseId, string chunkId)
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

        return searchService.TryGetChunk(courseId, chunkId, out var detail)
            ? Ok(detail)
            : NotFound();
    }

    [HttpGet("{courseId}/documents/{documentId}")]
    [HttpHead("{courseId}/documents/{documentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult OpenDocument(string courseId, string documentId)
    {
        if (!catalog.TryGetCourse(courseId, out var corpus) ||
            !corpus.TryResolveDocument(documentId, out var documentPath))
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(documentPath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".md" => "text/markdown; charset=utf-8",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };
        return PhysicalFile(documentPath, contentType, enableRangeProcessing: true);
    }

    [HttpGet("{courseId}/documents/{documentId}/pages/{pageNumber:int}/preview")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PreviewPage(
        string courseId,
        string documentId,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var previewPath = await pagePreviewService.GetOrCreateAsync(
                courseId,
                documentId,
                pageNumber,
                cancellationToken);
            return previewPath is null
                ? NotFound()
                : PhysicalFile(previewPath, "image/png");
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            return Problem(
                exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Course page preview is unavailable");
        }
    }

    private static string? NormalizeFilter(string? materialType) =>
        string.IsNullOrWhiteSpace(materialType) || materialType.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : materialType.Trim();
}
