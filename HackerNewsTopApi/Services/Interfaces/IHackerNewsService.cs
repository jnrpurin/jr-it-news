using HackerNewsTopApi.Models;
using HackerNewsTopApi.Models.DTOs;

namespace HackerNewsTopApi.Services.Interfaces
{
    public interface IHackerNewsService
    {
        Task<IList<StoryDto>> GetTopStoriesAsync(int count, CancellationToken ct = default);
        Task WarmupCacheAsync(CancellationToken ct = default);
    }
}
