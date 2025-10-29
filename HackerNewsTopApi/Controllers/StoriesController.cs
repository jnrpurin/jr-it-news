using Microsoft.AspNetCore.Mvc;
using HackerNewsTopApi.Services;

namespace HackerNewsTopApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StoriesController : ControllerBase
    {
        private readonly IHackerNewsService _hnService;
        private readonly ILogger<StoriesController> _logger;

        public StoriesController(IHackerNewsService hnService, ILogger<StoriesController> logger)
        {
            _hnService = hnService;
            _logger = logger;
        }

        // GET /api/stories?count=10
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int count = 10, CancellationToken ct = default)
        {
            if (count <= 0) return BadRequest(new { error = "count must be > 0" });
            if (count > 200) return BadRequest(new { error = "count too large (max 200)" });

            try
            {
                var top = await _hnService.GetTopStoriesAsync(count, ct);
                return Ok(top);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499); // client closed request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching top stories");
                return StatusCode(500, new { error = "Failed to fetch stories" });
            }
        }
    }
}
