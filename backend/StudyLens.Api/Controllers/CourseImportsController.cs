using Microsoft.AspNetCore.Mvc;
using StudyLens.Api.Contracts;
using StudyLens.Api.Services;

namespace StudyLens.Api.Controllers;

[ApiController]
[Route("api/courses/import")]
public sealed class CourseImportsController(CourseImportService importService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(300 * 1024 * 1024)]
    [ProducesResponseType<CourseStatusResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CourseStatusResponse>> Import(
        [FromForm] string courseName,
        [FromForm] List<IFormFile> files,
        [FromForm] List<string>? relativePaths,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await importService.ImportAsync(
                courseName ?? string.Empty,
                files,
                relativePaths,
                cancellationToken);
            return CreatedAtAction(
                nameof(CoursesController.GetStatus),
                "Courses",
                new { courseId = status.CourseId },
                status);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                exception.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Course could not be imported");
        }
    }
}
