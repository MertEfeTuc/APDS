using APDS.Events;

namespace APDS.Services.Notifications
{
    public interface INotificationPublisher
    {
        Task PublishAsync(INotificationEvent evt);
    }

    public class NotificationPublisher : INotificationPublisher
    {
        private readonly NotificationQueue _queue;

        public NotificationPublisher(NotificationQueue queue)
        {
            _queue = queue;
        }

        public async Task PublishAsync(INotificationEvent evt)
        {
            await _queue.EnqueueAsync(evt);
        }
    }
}