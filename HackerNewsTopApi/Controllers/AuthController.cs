using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using HackerNewsTopApi.Models;
using HackerNewsTopApi.Services;
using HackerNewsTopApi.Services.Interfaces;

namespace HackerNewsTopApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

         [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] LoginRequest request)
        {
            bool result = await _authService.RegisterAsync(request.Username, request.Password);
            if (!result)
                return BadRequest("User already exists.");

            return Ok("User registered successfully.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var token = await _authService.LoginAsync(request.Username, request.Password);
            if (token == null)
                return Unauthorized("Invalid credentials.");

            return Ok(new { token });
        }
    }
}
