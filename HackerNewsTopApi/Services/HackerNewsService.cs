using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using HackerNewsTopApi.Models;
using Polly.CircuitBreaker;
using HackerNewsTopApi.Services.Interfaces;
using HackerNewsTopApi.Models.DTOs;

namespace HackerNewsTopApi.Services
{
    public class HackerNewsService : IHackerNewsService
    {
        private readonly HttpClient _http;
        private readonly IDistributedCache _cache;
        private readonly ILogger<HackerNewsService> _logger;
        
        private const string BestStoriesUrl = "https://hacker-news.firebaseio.com/v0/beststories.json";
        private const string ItemUrlTemplate = "https://hacker-news.firebaseio.com/v0/item/{0}.json";
        private const string CacheKey_PreprocessedStories = "preprocessed_top_stories";
        private const int MaxStoriesToCache = 200; // Cache top 200 stories
        private const int CacheDurationMinutes = 2; // Cache for 2 minutes

        public HackerNewsService(
            HttpClient http,
            IDistributedCache cache,
            ILogger<HackerNewsService> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Returns the top N stories from the pre-processed CACHE
        /// </summary>
        public async Task<IList<StoryDto>> GetTopStoriesAsync(int count, CancellationToken ct = default)
        {
            if (count <= 0) return new List<StoryDto>();
            if (count > MaxStoriesToCache) count = MaxStoriesToCache;

            try
            {
                // Try to fetch from pre-processed cache
                var cachedData = await GetCachedStoriesAsync(ct);

                if (cachedData != null && cachedData.Stories.Any())
                {
                    var age = DateTime.UtcNow - cachedData.CachedAt;
                    _logger.LogInformation(
                        "Cache HIT! Returning {Count} of {Total} stories (cache age: {Age:F1}s)",
                        Math.Min(count, cachedData.Stories.Count),
                        cachedData.TotalStories,
                        age.TotalSeconds
                    );

                    // Simply return the first N from cache
                    return cachedData.Stories.Take(count).ToList();
                }

                // Cache miss - need to process
                _logger.LogWarning("Cache MISS! Processing stories from scratch...");
                await WarmupCacheAsync(ct);

                // Try again after warmup
                cachedData = await GetCachedStoriesAsync(ct);
                if (cachedData != null && cachedData.Stories.Any())
                {
                    return cachedData.Stories.Take(count).ToList();
                }

                _logger.LogError("Failed to get stories even after warmup");
                return new List<StoryDto>();
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError("Circuit Breaker OPEN. Attempting to return stale cache...");
                
                // Try to return expired cache as fallback
                var staleCache = await GetCachedStoriesAsync(ct, allowStale: true);
                if (staleCache != null && staleCache.Stories.Any())
                {
                    _logger.LogWarning("Returning EXPIRED cache as fallback");
                    return staleCache.Stories.Take(count).ToList();
                }

                throw new InvalidOperationException(
                    "Service temporarily unavailable. Please try again in a few seconds.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching stories");
                throw;
            }
        }

        /// <summary>
        /// Warms up the cache by fetching and processing the top 200 stories
        /// </summary>
        public async Task WarmupCacheAsync(CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Starting cache warmup...");

            try
            {
                // 1) Fetch best stories IDs
                var ids = await FetchBestStoriesIdsAsync(ct);
                if (ids == null || ids.Length == 0)
                {
                    _logger.LogWarning("No IDs returned by API");
                    return;
                }

                // 2) Take only the top N IDs to process
                var idsToFetch = ids.Take(MaxStoriesToCache).ToArray();
                _logger.LogInformation("Fetching details for {Count} stories...", idsToFetch.Length);

                // 3) Fetch details in parallel (limited to 10 concurrent)
                var items = await FetchStoriesInParallelAsync(idsToFetch, ct);

                // 4) Sort by score and create DTOs
                var sortedStories = items
                    .Where(i => i.score.HasValue)
                    .OrderByDescending(i => i.score!.Value)
                    .Select(MapToDto)
                    .ToList();

                // 5) Cache the pre-processed result
                var cachedData = new CachedStoriesData
                {
                    Stories = sortedStories,
                    CachedAt = DateTime.UtcNow,
                    TotalStories = sortedStories.Count
                };

                await SetCachedStoriesAsync(cachedData, ct);

                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Cache warmup completed! {Count} stories processed in {Elapsed:F2}s",
                    sortedStories.Count,
                    elapsed.TotalSeconds
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warmup");
                throw;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Fetches best stories IDs (with 30s cache)
        /// </summary>
        private async Task<int[]?> FetchBestStoriesIdsAsync(CancellationToken ct)
        {
            const string cacheKey = "beststories_ids";

            // Try to fetch from cache
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (!string.IsNullOrEmpty(cached))
            {
                _logger.LogDebug("IDs found in cache");
                return JsonSerializer.Deserialize<int[]>(cached);
            }

            // Cache miss - fetch from API
            _logger.LogInformation("Fetching IDs from HackerNews API...");
            var ids = await _http.GetFromJsonAsync<int[]>(BestStoriesUrl, ct);

            if (ids != null && ids.Length > 0)
            {
                // Cache for 30 seconds
                var serialized = JsonSerializer.Serialize(ids);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                };
                await _cache.SetStringAsync(cacheKey, serialized, options, ct);
                _logger.LogInformation("Received {Count} IDs", ids.Length);
            }

            return ids;
        }

        /// <summary>
        /// Fetches details of multiple stories in parallel
        /// </summary>
        private async Task<List<HnItem>> FetchStoriesInParallelAsync(int[] ids, CancellationToken ct)
        {
            var items = new List<HnItem>();
            var semaphore = new SemaphoreSlim(10); // Maximum 10 concurrent requests
            var failedCount = 0;

            var tasks = ids.Select(async id =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var item = await FetchSingleItemAsync(id, ct);
                    if (item != null && item.type == "story")
                    {
                        lock (items)
                        {
                            items.Add(item);
                        }
                    }
                    else if (item == null)
                    {
                        Interlocked.Increment(ref failedCount);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            if (failedCount > 0)
            {
                _logger.LogWarning("{FailedCount} items failed to fetch", failedCount);
            }

            return items;
        }

        /// <summary>
        /// Fetches details of a single item (with 5 minutes cache)
        /// </summary>
        private async Task<HnItem?> FetchSingleItemAsync(int id, CancellationToken ct)
        {
            string cacheKey = $"item_{id}";

            try
            {
                // Try to fetch from individual cache
                var cached = await _cache.GetStringAsync(cacheKey, ct);
                if (!string.IsNullOrEmpty(cached))
                {
                    return JsonSerializer.Deserialize<HnItem>(cached);
                }

                // Cache miss - fetch from API
                var url = string.Format(ItemUrlTemplate, id);
                var item = await _http.GetFromJsonAsync<HnItem?>(url, ct);

                if (item != null)
                {
                    // Cache for 5 minutes
                    var serialized = JsonSerializer.Serialize(item);
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    };
                    await _cache.SetStringAsync(cacheKey, serialized, options, ct);
                }

                return item;
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("Circuit breaker open when fetching item {Id}", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching item {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// Fetches the pre-processed stories cache
        /// </summary>
        private async Task<CachedStoriesData?> GetCachedStoriesAsync(
            CancellationToken ct, 
            bool allowStale = false)
        {
            try
            {
                var cached = await _cache.GetStringAsync(CacheKey_PreprocessedStories, ct);
                if (string.IsNullOrEmpty(cached))
                {
                    return null;
                }

                var data = JsonSerializer.Deserialize<CachedStoriesData>(cached);
                
                // Check if expired
                if (data != null && !allowStale)
                {
                    var age = DateTime.UtcNow - data.CachedAt;
                    if (age.TotalMinutes > CacheDurationMinutes)
                    {
                        _logger.LogWarning("Cache expired ({Age:F1} minutes)", age.TotalMinutes);
                        return null;
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading pre-processed cache");
                return null;
            }
        }

        /// <summary>
        /// Saves the pre-processed stories cache
        /// </summary>
        private async Task SetCachedStoriesAsync(CachedStoriesData data, CancellationToken ct)
        {
            try
            {
                var serialized = JsonSerializer.Serialize(data);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes + 1)
                };
                await _cache.SetStringAsync(CacheKey_PreprocessedStories, serialized, options, ct);
                
                _logger.LogInformation("Pre-processed cache saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving pre-processed cache");
                throw;
            }
        }

        private StoryDto MapToDto(HnItem item)
        {
            string timeIso = item.time.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(item.time.Value)
                    .ToString("yyyy-MM-dd'T'HH:mm:sszzz")
                : string.Empty;

            return new StoryDto
            {
                Title = item.title,
                Uri = item.url,
                PostedBy = item.by,
                Time = timeIso,
                Score = item.score ?? 0,
                CommentCount = item.descendants ?? 0
            };
        }

        #endregion
    }
}