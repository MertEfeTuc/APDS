namespace APDS.Models
{
    public enum NewsFetchMethod
    {
        Rss,
        Grounding   // RSS'i olmayan/çalışmayan kaynaklar için Gemini grounding (Google Search) ile arama
    }

    public class NewsSource
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // FetchMethod=Rss ise feed URL'i; FetchMethod=Grounding ise arama konusu/sorgusu
        public string Url { get; set; } = string.Empty;

        public NewsFetchMethod FetchMethod { get; set; } = NewsFetchMethod.Rss;

        public bool IsActive { get; set; } = true;
    }

    public class NewsItem
    {
        public int Id { get; set; }

        public int NewsSourceId { get; set; }
        public NewsSource NewsSource { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Link { get; set; } = string.Empty;   // dedup için unique

        public string? Summary { get; set; }

        public DateTime? PublishedDate { get; set; }

        public DateTime FetchedDate { get; set; } = DateTime.UtcNow;
    }
}
