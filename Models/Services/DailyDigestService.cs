using APDS.Events;
using APDS.Models;
using APDS.Services.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace APDS.Services
{
    public class DailyDigestService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private static readonly TimeSpan DigestTime = new TimeSpan(9, 0, 0); // 09:00

        public DailyDigestService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = now.Date + DigestTime;
                if (now > nextRun)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested) break;

                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                    var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

                    var reviewers = await userManager.GetUsersInRoleAsync("Reviewer");

                    foreach (var reviewer in reviewers)
                    {
                        var assignedAcademicianIds = await context.ReviewerAssignments
                            .Where(ra => ra.ReviewerId == reviewer.Id)
                            .Select(ra => ra.AcademicianId)
                            .ToListAsync(stoppingToken);

                        var ownPendingCount = await context.Activities
                            .CountAsync(a => assignedAcademicianIds.Contains(a.AcademicianId)
                                          && a.DelegatedReviewerId == null
                                          && (a.Status == ActivityStatus.SUBMITTED
                                           || a.Status == ActivityStatus.RESUBMITTED
                                           || a.Status == ActivityStatus.UNDER_REVIEW), stoppingToken);

                        var delegatedCount = await context.Activities
                            .CountAsync(a => a.DelegatedReviewerId == reviewer.Id
                                          && (a.Status == ActivityStatus.SUBMITTED
                                           || a.Status == ActivityStatus.RESUBMITTED
                                           || a.Status == ActivityStatus.UNDER_REVIEW), stoppingToken);

                        var totalPending = ownPendingCount + delegatedCount;

                        if (totalPending == 0) continue;

                        var message = delegatedCount > 0
                            ? $"Bugün {totalPending} bekleyen inceleme var ({delegatedCount} tanesi size devredilmiş)."
                            : $"Bugün {totalPending} bekleyen inceleme var.";

                        await publisher.PublishAsync(new ActivityStatusChangedEvent(
                            RecipientUserId: reviewer.Id,
                            Type: NotificationType.REMINDER,
                            Message: message,
                            RelatedEntityId: null
                        ));
                    }
                }
            }
        }
    }
}