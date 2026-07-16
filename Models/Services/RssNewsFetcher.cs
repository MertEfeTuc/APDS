using System.ServiceModel.Syndication;
using System.Xml;

namespace APDS.Models.Services
{
    public class RssNewsFetcher : INewsFetcher
    {
        private readonly HttpClient _httpClient;

        public RssNewsFetcher(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public NewsFetchMethod FetchMethod => NewsFetchMethod.Rss;

        public async Task<List<FetchedNewsItem>> FetchAsync(NewsSource source, CancellationToken ct)
        {
            var result = new List<FetchedNewsItem>();

            using var stream = await _httpClient.GetStreamAsync(source.Url, ct);
            using var reader = XmlReader.Create(stream);
            var feed = SyndicationFeed.Load(reader);

            foreach (var item in feed.Items)
            {
                var link = item.Links.FirstOrDefault()?.Uri?.ToString() ?? item.Id;
                if (string.IsNullOrEmpty(link) || !UrlSafety.IsHttpUrl(link))
                    continue;

                result.Add(new FetchedNewsItem(
                    Title: item.Title?.Text ?? string.Empty,
                    Link: link,
                    Summary: item.Summary?.Text,
                    PublishedDate: item.PublishDate != default ? item.PublishDate.UtcDateTime : null
                ));
            }

            return result;
        }
    }
}
