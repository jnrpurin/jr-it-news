using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Distributed;
using HackerNewsTopApi.Models;

namespace HackerNewsTopApi.Services
{
    public class HackerNewsService : IHackerNewsService
    {
        private readonly HttpClient _http;
        private readonly IDistributedCache _cache;
        private const string BestStoriesUrl = "https://hacker-news.firebaseio.com/v0/beststories.json";
        private const string ItemUrlTemplate = "https://hacker-news.firebaseio.com/v0/item/{0}.json";

        public HackerNewsService(HttpClient http, IDistributedCache cache)
        {
            _http = http;
            _cache = cache;
        }

        public async Task<IList<StoryDto>> GetTopStoriesAsync(int count, CancellationToken ct = default)
        {
            if (count <= 0) return new List<StoryDto>();

            const int MaxAllowed = 200;
            if (count > MaxAllowed) count = MaxAllowed;

            // 1) Get IDs from cache (30s) or API
            var ids = await GetOrCreateCacheAsync(
                "beststories_ids",
                async () =>
                {
                    var result = await _http.GetFromJsonAsync<int[]>(BestStoriesUrl, ct);
                    return result ?? Array.Empty<int>();
                },
                TimeSpan.FromSeconds(30),
                ct
            );

            if (ids == null || ids.Length == 0) return new List<StoryDto>();

            // 2) Calculate how many items to fetch
            int factor = 5;
            int toFetch = Math.Min(ids.Length, Math.Max(count * factor, 50));
            var idsToFetch = ids.Take(toFetch).ToArray();

            // 3) Get details in paralel
            var items = new List<HnItem>();
            var semaphore = new SemaphoreSlim(10); // concurency limitation
            var tasks = idsToFetch.Select(async id =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    string cacheKey = $"item_{id}";
                    var item = await GetOrCreateCacheAsync(
                        cacheKey,
                        async () =>
                        {
                            var url = string.Format(ItemUrlTemplate, id);
                            try
                            {
                                return await _http.GetFromJsonAsync<HnItem?>(url, ct);
                            }
                            catch
                            {
                                return null;
                            }
                        },
                        TimeSpan.FromMinutes(5),
                        ct
                    );

                    if (item != null && item.type == "story")
                    {
                        lock (items) items.Add(item);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // 4) Order by score and return top 'N'
            var top = items
                .Where(i => i.score.HasValue)
                .OrderByDescending(i => i.score!.Value)
                .Take(count)
                .Select(MapToDto)
                .ToList();

            return top;
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

        private async Task<T?> GetOrCreateCacheAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan expiration,
            CancellationToken ct = default)
        {
            // Try to get from cache
            var cachedData = await _cache.GetStringAsync(key, ct);
            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonSerializer.Deserialize<T>(cachedData);
            }

            // If not in cache, fetch and store
            var data = await factory();
            if (data != null)
            {
                var serialized = JsonSerializer.Serialize(data);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration
                };
                await _cache.SetStringAsync(key, serialized, options, ct);
            }

            return data;
        }
    }
}
