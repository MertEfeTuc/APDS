namespace APDS.Models.Services
{
    public record FetchedNewsItem(string Title, string Link, string? Summary, DateTime? PublishedDate);

    public interface INewsFetcher
    {
        NewsFetchMethod FetchMethod { get; }

        Task<List<FetchedNewsItem>> FetchAsync(NewsSource source, CancellationToken ct);
    }
}
