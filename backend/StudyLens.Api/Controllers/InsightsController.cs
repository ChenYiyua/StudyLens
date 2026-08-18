using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using StudyLens.Api.Contracts;
using StudyLens.Api.Services;

namespace StudyLens.Api.Controllers;

[ApiController]
[Route("api/insights")]
public sealed class InsightsController(LearningInsightsService insightsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<InsightsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InsightsResponse>> Get(
        [FromQuery, Required, StringLength(64, MinimumLength = 8)] string participantId,
        CancellationToken cancellationToken) =>
        Ok(await insightsService.GetAsync(participantId.Trim(), cancellationToken));
}
