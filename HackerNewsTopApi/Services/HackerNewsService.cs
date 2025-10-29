using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using HackerNewsTopApi.Models;

namespace HackerNewsTopApi.Services
{
    public class HackerNewsService : IHackerNewsService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private const string BestStoriesUrl = "https://hacker-news.firebaseio.com/v0/beststories.json";
        private const string ItemUrlTemplate = "https://hacker-news.firebaseio.com/v0/item/{0}.json";

        public HackerNewsService(HttpClient http, IMemoryCache cache)
        {
            _http = http;
            _cache = cache;
        }

        public async Task<IList<StoryDto>> GetTopStoriesAsync(int count, CancellationToken ct = default)
        {
            if (count <= 0) return new List<StoryDto>();

            const int MaxAllowed = 200;
            if (count > MaxAllowed) count = MaxAllowed;

            // 1) get IDs (caching 30s)
            var ids = await _cache.GetOrCreateAsync("beststories_ids", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                var result = await _http.GetFromJsonAsync<int[]>(BestStoriesUrl, ct);
                return result ?? Array.Empty<int>();
            });

            if (ids == null || ids.Length == 0) return new List<StoryDto>();

            // 2) get details only for first N IDs from HN,
            // but they may do not have high score;
            // so I get the details for the firsts M IDs (M = max(count * factor, someMinimum))
            // using factor 5.
            int factor = 5;
            int toFetch = Math.Min(ids.Length, Math.Max(count * factor, 50));
            var idsToFetch = ids.Take(toFetch).ToArray();

            // 3) get details in paralel
            var items = new List<HnItem>();
            var semaphore = new SemaphoreSlim(10); // concurency limitation
            var tasks = idsToFetch.Select(async id =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    string cacheKey = $"item_{id}";
                    var item = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                        var url = string.Format(ItemUrlTemplate, id);
                        try
                        {
                            return await _http.GetFromJsonAsync<HnItem?>(url, ct);
                        }
                        catch
                        {
                            return null;
                        }
                    });

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

            // 4) order score desc and get top `count`
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
    }
}
