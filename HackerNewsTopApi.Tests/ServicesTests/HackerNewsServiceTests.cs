using Moq;
using Moq.Protected;
using Xunit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HackerNewsTopApi.Models;
using HackerNewsTopApi.Services;

namespace HackerNewsTopApi.Tests.ServicesTests
{
    public class HackerNewsServiceTests
    {
        private readonly Mock<IDistributedCache> _mockCache;
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<HackerNewsService>> _mockLogger;
        private readonly HackerNewsService _hnService;

        public HackerNewsServiceTests()
        {
            _mockCache = new Mock<IDistributedCache>();
            _mockHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHandler.Object);
            _mockLogger = new Mock<ILogger<HackerNewsService>>();
            _hnService = new HackerNewsService(_httpClient, _mockCache.Object, _mockLogger.Object);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.AbsoluteUri.Contains("beststories.json")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[123, 456]", Encoding.UTF8, "application/json") 
                });
    
    
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.AbsoluteUri.Contains("item/")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"id\":123, \"title\":\"Mocked Story\"}", Encoding.UTF8, "application/json") 
                });
        }

        [Fact]
        public async Task GetTopStoriesAsync_CacheHit_ReturnsStoriesFromCache()
        {
            var expectedStories = new List<StoryDto>
            {
                new StoryDto 
                { 
                    Title = "Cached Story 1", 
                    Uri = "http://uri1.com", 
                    PostedBy = "user1", 
                    Time = "2025-01-01T00:00:00+00:00", 
                    Score = 100, 
                    CommentCount = 10 
                },
                new StoryDto 
                { 
                    Title = "Cached Story 2", 
                    Uri = "http://uri2.com", 
                    PostedBy = "user2", 
                    Time = "2025-01-01T00:01:00+00:00", 
                    Score = 200, 
                    CommentCount = 20 
                }
            };

            var cachedData = new CachedStoriesData
            { 
                Stories = expectedStories,
                CachedAt = DateTime.UtcNow, 
                TotalStories = expectedStories.Count 
            };
            var serializedData = JsonSerializer.Serialize(cachedData);
            var cachedBytes = Encoding.UTF8.GetBytes(serializedData); 

            _mockCache.Setup(c => c.GetAsync(
                "preprocessed_top_stories", 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedBytes); 

            // Act
            var result = await _hnService.GetTopStoriesAsync(10);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Cached Story 1", result[0].Title);

            // Verify
            _mockHandler.Protected().Verify(
                "SendAsync",
                Moq.Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task GetTopStoriesAsync_CacheMissButWarmupFails_ThrowsHttpRequestException()
        {
            // Arrange
            _mockCache.Setup(c => c.GetAsync(
                "preprocessed_top_stories",
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.AbsoluteUri.Contains("beststories.json")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("")
                });

            // Act & Assert
            await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(
                () => _hnService.GetTopStoriesAsync(10)
            );
            
            _mockHandler.Protected().Verify(
                "SendAsync",
                Times.AtLeastOnce(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }
    }
}
