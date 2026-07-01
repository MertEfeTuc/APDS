using APDS.Models;
using Microsoft.AspNetCore.Identity;

namespace APDS.Services.Notifications
{
    public class NotificationProcessor : BackgroundService
    {
        private readonly NotificationQueue _queue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationProcessor> _logger;

        public NotificationProcessor(
            NotificationQueue queue,
            IServiceProvider serviceProvider,
            ILogger<NotificationProcessor> logger)
        {
            _queue = queue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var evt in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                    var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                    context.Notifications.Add(new Notification
                    {
                        UserId = evt.RecipientUserId,
                        Type = evt.Type,
                        Message = evt.Message,
                        RelatedEntityId = evt.RelatedEntityId
                    });
                    await context.SaveChangesAsync(stoppingToken);

                    var recipient = await userManager.FindByIdAsync(evt.RecipientUserId);
                    if (!string.IsNullOrEmpty(recipient?.Email))
                    {
                        await emailSender.SendAsync(recipient.Email, "APDS Bildirimi", evt.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bildirim işlenirken hata oluştu. Alıcı: {RecipientUserId}, Mesaj: {Message}",
                        evt.RecipientUserId, evt.Message);
                }
            }
        }
    }
}