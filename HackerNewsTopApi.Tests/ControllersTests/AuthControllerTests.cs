using Moq;
using Xunit;
using HackerNewsTopApi.Controllers;
using HackerNewsTopApi.Services.Interfaces;
using HackerNewsTopApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Reflection;

namespace HackerNewsTopApi.Tests.ControllersTests
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockAuthService = new Mock<IAuthService>();
            _controller = new AuthController(_mockAuthService.Object);
        }

        [Fact]
        public async Task Register_NewUser_ReturnsOk()
        {
            // Arrange
            var request = new LoginRequest { Username = "newuser", Password = "password" };

            _mockAuthService.Setup(s => s.RegisterAsync(
                request.Username,
                request.Password))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Register(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Register_ExistingUser_ReturnsBadRequest()
        {
            // Arrange
            var request = new LoginRequest { Username = "existinguser", Password = "password" };

            _mockAuthService.Setup(s => s.RegisterAsync(
                request.Username,
                request.Password))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Register(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOkWithToken()
        {
            // Arrange
            var request = new LoginRequest { Username = "validuser", Password = "password" };
            string expectedToken = "valid_jwt_token";

            _mockAuthService.Setup(s => s.LoginAsync(
                request.Username,
                request.Password))
                .ReturnsAsync(expectedToken);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedObject = okResult.Value!;
            var tokenProperty = returnedObject.GetType().GetProperty("token");
            
            Assert.NotNull(tokenProperty);
            
            var actualToken = tokenProperty!.GetValue(returnedObject);
            Assert.Equal(expectedToken, actualToken);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequest { Username = "invaliduser", Password = "wrongpassword" };

            _mockAuthService.Setup(s => s.LoginAsync(
                request.Username,
                request.Password))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _controller.Login(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}