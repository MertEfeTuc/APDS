using Microsoft.EntityFrameworkCore;

namespace APDS.Models.Services
{
    public class NewsFetchService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(3);

        public NewsFetchService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var fetchers = scope.ServiceProvider.GetServices<INewsFetcher>().ToList();

                    var sources = await context.NewsSources
                        .Where(s => s.IsActive)
                        .ToListAsync(stoppingToken);

                    foreach (var source in sources)
                    {
                        var fetcher = fetchers.FirstOrDefault(f => f.FetchMethod == source.FetchMethod);
                        if (fetcher == null)
                            continue;

                        List<FetchedNewsItem> fetchedItems;
                        try
                        {
                            fetchedItems = await fetcher.FetchAsync(source, stoppingToken);
                        }
                        catch
                        {
                            continue; // bir kaynak başarısız olursa diğerlerini engellemesin
                        }

                        var existingLinks = await context.NewsItems
                            .Where(ni => ni.NewsSourceId == source.Id)
                            .Select(ni => ni.Link)
                            .ToListAsync(stoppingToken);

                        var newItems = fetchedItems
                            .Where(fi => !existingLinks.Contains(fi.Link))
                            .Select(fi => new NewsItem
                            {
                                NewsSourceId = source.Id,
                                Title = fi.Title,
                                Link = fi.Link,
                                Summary = fi.Summary,
                                PublishedDate = fi.PublishedDate
                            });

                        context.NewsItems.AddRange(newItems);
                    }

                    await context.SaveChangesAsync(stoppingToken);
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
    }
}
