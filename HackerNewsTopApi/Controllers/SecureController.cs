using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HackerNewsTopApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecureController : ControllerBase
    {
        [HttpGet("secret")]
        [Authorize]
        public IActionResult GetSecret()
        {
            var user = User.Identity?.Name ?? "Unknown";
            return Ok(new { message = $"HI, {user}! You access an protected endpoint." });
        }
    }
}
