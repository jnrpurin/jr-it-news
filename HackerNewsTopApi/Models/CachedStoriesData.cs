using HackerNewsTopApi.Models.DTOs;

namespace HackerNewsTopApi.Models
{
    /// <summary>
    /// It represents the pre-processed cache of the top stories.
    /// </summary>
    public class CachedStoriesData
    {
        public List<StoryDto> Stories { get; set; } = [];
        public DateTime CachedAt { get; set; }
        public int TotalStories { get; set; }
    }
}