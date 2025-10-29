namespace HackerNewsTopApi.Models
{
    public class HnItem
    {
        public int id { get; set; }
        public string? by { get; set; }
        public long? time { get; set; } // unix time (seconds)
        public string? title { get; set; }
        public string? url { get; set; }
        public int? score { get; set; }
        public int? descendants { get; set; }
        public string? type { get; set; }
    }
}
