using Microsoft.AspNetCore.Mvc;
using HackerNewsTopApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace HackerNewsTopApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("HackerNewsRateLimitP1")]
    [Authorize]
    public class StoriesController : ControllerBase
    {
        private readonly IHackerNewsService _hnService;
        private readonly ILogger<StoriesController> _logger;

        public StoriesController(IHackerNewsService hnService, ILogger<StoriesController> logger)
        {
            _hnService = hnService;
            _logger = logger;
        }

        /// <summary>
        /// Returns the main stories from Hacker News.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status499ClientClosedRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any)]
        public async Task<ActionResult<IEnumerable<object>>> GetTopStories([FromQuery][Range(1, 200)] int count = 10, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var top = await _hnService.GetTopStoriesAsync(count, ct);

                if (top == null || !top.Any())
                    return NotFound(Problem(
                        title: "No stories found",
                        statusCode: StatusCodes.Status404NotFound,
                        detail: "No stories were returned from Hacker News."));

                // Head to track it
                Response.Headers["X-Request-ID"] = Guid.NewGuid().ToString("N");

                return Ok(top);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, Problem(
                    title: "Client closed request",
                    statusCode: 499,
                    detail: "The client cancelled the request."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching top stories");
                return Problem(
                    title: "Failed to fetch stories",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Forces manual heating of the story cache.
        /// </summary>
        [HttpPost("cache:warmup")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> WarmupCache(CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Manual cache warmup requested");
                await _hnService.WarmupCacheAsync(ct);

                return Ok(new
                {
                    message = "Cache warmup completed successfully",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual cache warmup");
                return Problem(
                    title: "Cache warmup failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
