namespace HackerNewsTopApi.Models
{
    public class StoryDto
    {
        public string? Title { get; set; }
        public string? Uri { get; set; }
        public string? PostedBy { get; set; }
        public string? Time { get; set; } // ISO 8601 string
        public int Score { get; set; }
        public int CommentCount { get; set; }
    }
}
