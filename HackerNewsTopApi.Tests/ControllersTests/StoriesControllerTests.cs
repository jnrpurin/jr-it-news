using Moq;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HackerNewsTopApi.Controllers;
using HackerNewsTopApi.Services.Interfaces;
using HackerNewsTopApi.Models.DTOs;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

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

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };            
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(20)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(200)]
        public async Task GetTopStories_VariousValidCounts_ReturnsOkWithRequestedCount(int count)
        {
            // Arrange
            var mockStories = Enumerable.Range(1, count)
                .Select(i => new StoryDto 
                { 
                    Title = $"Story {i}",
                    Score = 1000 - i
                })
                .ToList();

            _mockHnService
                .Setup(s => s.GetTopStoriesAsync(count, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IList<StoryDto>>(mockStories));

            // Act
            var result = await _controller.GetTopStories(count);

            // Assert
            var objectResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, objectResult.StatusCode);

            var stories = Assert.IsAssignableFrom<IEnumerable<StoryDto>>(objectResult.Value);
            var storiesList = stories.ToList();

            Assert.Equal(count, storiesList.Count);
            Assert.Equal("Story 1", storiesList[0].Title);
            Assert.Equal($"Story {count}", storiesList[count - 1].Title);

            _mockHnService.Verify(
                s => s.GetTopStoriesAsync(count, It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task GetTopStories_ValidRequest_ReturnsOkWithStories()
        {
            // Arrange
            int requestedCount = 10;
            var mockStories = new List<StoryDto>
            {
                new StoryDto { Title = "Story 1", Score = 100, },
                new StoryDto { Title = "Story 2", Score = 90,  },
                new StoryDto { Title = "Story 3", Score = 80,  },
                new StoryDto { Title = "Story 4", Score = 70,  },
                new StoryDto { Title = "Story 5", Score = 60,  },
                new StoryDto { Title = "Story 6", Score = 50,  },
                new StoryDto { Title = "Story 7", Score = 40,  },
                new StoryDto { Title = "Story 8", Score = 30,  },
                new StoryDto { Title = "Story 9", Score = 20,  },
                new StoryDto { Title = "Story 10",Score = 10,  }
            };

            _mockHnService
                .Setup(s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IList<StoryDto>>(mockStories));

            // Act
            var result = await _controller.GetTopStories(requestedCount);

            // Assert
            var objectResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(200, objectResult.StatusCode);

            var stories = Assert.IsAssignableFrom<IEnumerable<StoryDto>>(objectResult.Value);
            var storiesList = stories.ToList();

            Assert.Equal(10, storiesList.Count);
            Assert.Equal("Story 1", storiesList[0].Title);
            Assert.Equal("Story 10", storiesList[9].Title);
            Assert.Equal(100, storiesList[0].Score);

            // Verify service was called with correct parameters
            _mockHnService.Verify(
                s => s.GetTopStoriesAsync(requestedCount, It.IsAny<CancellationToken>()),
                Times.Once
            );

            // Verify X-Request-ID header was added
            Assert.True(_controller.Response.Headers.ContainsKey("X-Request-ID"));
            var requestId = _controller.Response.Headers["X-Request-ID"].ToString();
            Assert.NotEmpty(requestId);
            Assert.Equal(32, requestId.Length); // GUID without hyphens
        }        

        [Fact]
        public async Task Get_CountLessThanOrEqualToZero_ReturnsNotFoundWithProblemDetails()
        {
            // Arrange
            _mockHnService
                .Setup(s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IList<StoryDto>?)null);

            // Act
            var result = await _controller.GetTopStories(count: 0);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var objectResult = Assert.IsType<ObjectResult>(notFoundResult.Value);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

            Assert.Equal(404, problem.Status);
            Assert.Equal("No stories found", problem.Title);

            _mockHnService.Verify(
                s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Get_CountTooLarge_ReturnsNotFoundWithProblemDetails()
        {
            // Arrange
            _mockHnService
                .Setup(s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IList<StoryDto>?)null);

            // Act
            var result = await _controller.GetTopStories(count: 201);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var objectResult = Assert.IsType<ObjectResult>(notFoundResult.Value);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

            Assert.Equal(404, problem.Status);
            Assert.Equal("No stories found", problem.Title);

            _mockHnService.Verify(
                s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Get_ServiceThrowsOperationCanceledException_ReturnsStatusCode499()
        {
            // Arrange
            _mockHnService
                .Setup(s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var result = await _controller.GetTopStories();

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(499, objectResult.StatusCode);

            var innerObjectResult = Assert.IsType<ObjectResult>(objectResult.Value);
            var problem = Assert.IsType<ProblemDetails>(innerObjectResult.Value);
            Assert.Equal("Client closed request", problem.Title);
            Assert.Equal(499, problem.Status);
        }

        [Fact]
        public async Task Get_ServiceThrowsGenericException_ReturnsStatusCode500()
        {
            // Arrange
            _mockHnService
                .Setup(s => s.GetTopStoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _controller.GetTopStories();

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, objectResult.StatusCode);

            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("Failed to fetch stories", problem.Title);
            Assert.Equal(500, problem.Status);
        }

        [Fact]
        public async Task WarmupCache_Success_ReturnsOk()
        {
            // Arrange
            _mockHnService
                .Setup(s => s.WarmupCacheAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.WarmupCache();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            var value = okResult.Value!;
            var messageProp = value.GetType().GetProperty("message");
            var timestampProp = value.GetType().GetProperty("timestamp");

            Assert.NotNull(messageProp);
            Assert.NotNull(timestampProp);

            Assert.Equal("Cache warmup completed successfully", messageProp!.GetValue(value));
            Assert.NotNull(timestampProp!.GetValue(value));

            _mockHnService.Verify(
                s => s.WarmupCacheAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task WarmupCache_ThrowsException_ReturnsStatusCode500()
        {
            // Arrange
            _mockHnService
                .Setup(s => s.WarmupCacheAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Redis is unavailable"));

            // Act
            var result = await _controller.WarmupCache();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);

            var problem = Assert.IsType<ProblemDetails>(statusCodeResult.Value);
            Assert.Equal("Cache warmup failed", problem.Title);
            Assert.Equal(500, problem.Status);

            _mockHnService.Verify(
                s => s.WarmupCacheAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}