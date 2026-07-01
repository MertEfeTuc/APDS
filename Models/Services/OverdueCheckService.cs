using APDS.Events;
using APDS.Models;
using APDS.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace APDS.Services
{
    public class OverdueCheckService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private const int OverdueDays = 7;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        public OverdueCheckService(IServiceProvider serviceProvider)
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
                    var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

                    var threshold = DateTime.UtcNow.AddDays(-OverdueDays);

                    var overdueActivities = await context.Activities
                        .Where(a => !a.OverdueNotificationSent
                                 && a.LastStatusChangeDate < threshold
                                 && (a.Status == ActivityStatus.SUBMITTED
                                  || a.Status == ActivityStatus.RESUBMITTED
                                  || a.Status == ActivityStatus.UNDER_REVIEW))
                        .ToListAsync(stoppingToken);

                    foreach (var activity in overdueActivities)
                    {
                        var reviewerId = activity.DelegatedReviewerId
                            ?? await context.ReviewerAssignments
                                .Where(ra => ra.AcademicianId == activity.AcademicianId)
                                .Select(ra => ra.ReviewerId)
                                .FirstOrDefaultAsync(stoppingToken);

                        if (reviewerId != null)
                        {
                            await publisher.PublishAsync(new ActivityStatusChangedEvent(
                                RecipientUserId: reviewerId,
                                Type: NotificationType.REMINDER,
                                Message: $"\"{activity.Title}\" faaliyeti {OverdueDays} günden uzun süredir incelenmeyi bekliyor.",
                                RelatedEntityId: activity.Id
                            ));

                            activity.OverdueNotificationSent = true;
                        }
                    }

                    if (overdueActivities.Any())
                        await context.SaveChangesAsync(stoppingToken);
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
    }
}