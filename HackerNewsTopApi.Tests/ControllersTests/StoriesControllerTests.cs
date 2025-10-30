using Moq;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HackerNewsTopApi.Controllers;
using HackerNewsTopApi.Services.Interfaces;
using HackerNewsTopApi.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Reflection;

namespace HackerNewsTopApi.Tests.ControllersTests
{
    public class StoriesControllerTests
    {
        private readonly Mock<IHackerNewsService> _mockHnService;
        private readonly Mock<ILogger<StoriesController>> _mockLogger;
        private readonly StoriesController _controller;

        public StoriesControllerTests()
        {
            _mockHnService = new Mock<IHackerNewsService>();
            _mockLogger = new Mock<ILogger<StoriesController>>();
            _controller = new StoriesController(_mockHnService.Object, _mockLogger.Object);
        }

        #region GET /api/stories

        [Fact]
        public async Task Get_CountLessThanOrEqualToZero_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Get(count: 0);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var returnedObject = badRequestResult.Value!;

            var errorProperty = returnedObject.GetType().GetProperty("error");
            Assert.NotNull(errorProperty);
            
            var actualError = errorProperty!.GetValue(returnedObject);
            Assert.Equal("count must be > 0", actualError); 

            _mockHnService.Verify(s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Get_CountTooLarge_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.Get(count: 201);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var returnedObject = badRequestResult.Value!;

            var errorProperty = returnedObject.GetType().GetProperty("error");
            var actualError = errorProperty!.GetValue(returnedObject);
            Assert.Equal("count too large (max 200)", actualError);

            _mockHnService.Verify(s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Get_ValidCount_ReturnsOkWithStories()
        {
            // Arrange
            var mockStories = new List<StoryDto> { new StoryDto { Title = "Test Story" } };

            _mockHnService.Setup(s => s.GetTopStoriesAsync(
                10, 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockStories);

            // Act
            var result = await _controller.Get();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<List<StoryDto>>(okResult.Value);
            Assert.Single(returnValue);
            Assert.Equal("Test Story", returnValue[0].Title);

            _mockHnService.Verify(s => s.GetTopStoriesAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Get_ServiceThrowsOperationCanceledException_ReturnsStatusCode499()
        {
            // Arrange
            _mockHnService.Setup(s => s.GetTopStoriesAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var result = await _controller.Get();

            // Assert
            var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(499, statusCodeResult.StatusCode); // 499 - Client Closed Request
        }

        [Fact]
        public async Task Get_ServiceThrowsGenericException_ReturnsStatusCode500()
        {
            // Arrange
            _mockHnService.Setup(s => s.GetTopStoriesAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _controller.Get();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        #endregion

        #region POST /api/stories/warmup

        [Fact]
        public async Task WarmupCache_Success_ReturnsOk()
        {
            // Arrange
            _mockHnService.Setup(s => s.WarmupCacheAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.WarmupCache();

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockHnService.Verify(s => s.WarmupCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WarmupCache_ThrowsException_ReturnsStatusCode500()
        {
            // Arrange
            _mockHnService.Setup(s => s.WarmupCacheAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis is unavailable"));

            // Act
            var result = await _controller.WarmupCache();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);

            _mockHnService.Verify(s => s.WarmupCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}