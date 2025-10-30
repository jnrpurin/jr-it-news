using HackerNewsTopApi.Models;

namespace HackerNewsTopApi.Services
{
    public interface IHackerNewsService
    {
        Task<IList<StoryDto>> GetTopStoriesAsync(int count, CancellationToken ct = default);
        Task WarmupCacheAsync(CancellationToken ct = default);
    }
}
