using Moq;
using Xunit;
using HackerNewsTopApi.Services;
using HackerNewsTopApi.Infrastructure.Interfaces;
using HackerNewsTopApi.Domain;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace HackerNewsTopApi.Tests.ServicesTests
{

    public class AuthServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockConfiguration = new Mock<IConfiguration>();

            var configSection = new Mock<IConfigurationSection>();

            configSection.SetupGet(c => c["Key"]).Returns("SuperSecretKeyThatIsAtLeast256BitsLong");
            configSection.SetupGet(c => c["Issuer"]).Returns("TestIssuer");
            configSection.SetupGet(c => c["Audience"]).Returns("TestAudience");
            
            _mockConfiguration.Setup(c => c.GetSection("Jwt")).Returns(configSection.Object);

            string passwordHash = BCrypt.Net.BCrypt.HashPassword("senhaforte123");

            var existingUser = new User { Id = 1, Username = "testuser", PasswordHash = passwordHash };

            _mockUserRepository.Setup(r => r.GetByUsernameAsync("testuser"))
                               .ReturnsAsync(existingUser);

            _mockUserRepository.Setup(r => r.GetByUsernameAsync("nonexistent"))
                               .ReturnsAsync((User?)null);

            _authService = new AuthService(_mockUserRepository.Object, _mockConfiguration.Object);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsToken()
        {
            // Act
            var token = await _authService.LoginAsync("testuser", "senhaforte123");

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public async Task LoginAsync_InvalidPassword_ReturnsNull()
        {
            // Act
            var token = await _authService.LoginAsync("testuser", "senhainvalida");

            // Assert
            Assert.Null(token);
        }

        [Fact]
        public async Task RegisterAsync_NewUser_ReturnsTrueAndAddsUser()
        {
            // Arrange
            _mockUserRepository.Setup(r => r.GetByUsernameAsync("newuser"))
                               .ReturnsAsync((User?)null); 

            // Act
            var result = await _authService.RegisterAsync("newuser", "novasenha");

            // Assert
            Assert.True(result);
            _mockUserRepository.Verify(r => r.AddUserAsync(It.IsAny<User>()), Times.Once);
        }
    }
}