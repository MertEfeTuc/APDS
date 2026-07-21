using APDS.Events;
using APDS.Models;
using APDS.Models.Services;
using APDS.Services.Notifications;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace APDS.Services.PlagiarismCheck
{
    public class PlagiarismCheckProcessor : BackgroundService
    {
        private readonly PlagiarismCheckQueue _queue;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;
        private readonly ILogger<PlagiarismCheckProcessor> _logger;

        public PlagiarismCheckProcessor(
            PlagiarismCheckQueue queue,
            IServiceProvider serviceProvider,
            IConfiguration config,
            ILogger<PlagiarismCheckProcessor> logger)
        {
            _queue = queue;
            _serviceProvider = serviceProvider;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var checkService = scope.ServiceProvider.GetRequiredService<IPlagiarismCheckService>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                    var notificationPublisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

                    var check = await context.PlagiarismChecks.FindAsync(new object[] { job.PlagiarismCheckId }, stoppingToken);
                    if (check == null)
                    {
                        _logger.LogWarning("PlagiarismCheck bulunamadı: Id={PlagiarismCheckId}", job.PlagiarismCheckId);
                        continue;
                    }

                    var activity = await context.Activities.FindAsync(new object[] { job.ActivityId }, stoppingToken);
                    if (activity == null)
                    {
                        _logger.LogWarning("Activity bulunamadı: Id={ActivityId}", job.ActivityId);
                        check.Status = PlagiarismCheckStatus.Failed;
                        check.ErrorMessage = "İlişkili activity bulunamadı";
                        await context.SaveChangesAsync(stoppingToken);
                        continue;
                    }

                    check.Status = PlagiarismCheckStatus.Running;
                    await context.SaveChangesAsync(stoppingToken);

                    var result = await checkService.CheckAsync(activity.Title, activity.Description);

                    if (!result.Success)
                    {
                        check.Status = PlagiarismCheckStatus.Failed;
                        check.ErrorMessage = result.ErrorMessage;
                        await context.SaveChangesAsync(stoppingToken);
                        continue;
                    }

                    check.Status = PlagiarismCheckStatus.Completed;
                    check.SimilarityScore = result.SimilarityScore;
                    check.FoundSourcesJson = JsonConvert.SerializeObject(result.FoundSources);
                    check.CheckedDate = DateTime.UtcNow;
                    await context.SaveChangesAsync(stoppingToken);

                    var threshold = _config.GetValue<double?>("PlagiarismCheck:HighSimilarityThreshold") ?? 0.7;
                    if (check.SimilarityScore.HasValue && check.SimilarityScore.Value >= threshold)
                    {
                        // ROL ADI VARSAYIMI: "Admin" - ReviewController'daki "Reviewer" rol string'i teyit edildi,
                        // Admin için de aynı şekilde literal "Admin" olduğunu varsayıyorum.
                        var admins = await userManager.GetUsersInRoleAsync("Admin");
                        var scorePercent = (check.SimilarityScore.Value * 100).ToString("0");

                        foreach (var admin in admins)
                        {
                            await notificationPublisher.PublishAsync(new ActivityStatusChangedEvent(
                                RecipientUserId: admin.Id,
                                Type: NotificationType.PLAGIARISM_DETECTED,
                                Message: $"\"{activity.Title}\" başlıklı aktivitede yüksek benzerlik tespit edildi (%{scorePercent}).",
                                RelatedEntityId: activity.Id
                            ));
                        }

                        _logger.LogInformation(
                            "Yüksek benzerlik tespit edildi ve {Count} Admin'e bildirim gönderildi. ActivityId={ActivityId}, Score={Score}",
                            admins.Count, job.ActivityId, check.SimilarityScore);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Plagiarism check işlenirken hata oluştu. PlagiarismCheckId={PlagiarismCheckId}", job.PlagiarismCheckId);
                }
            }
        }
    }
}