using System.Threading.Channels;
using APDS.Events;

namespace APDS.Services.Notifications
{
    public class NotificationQueue
    {
        private readonly Channel<INotificationEvent> _channel =
            Channel.CreateUnbounded<INotificationEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        public ValueTask EnqueueAsync(INotificationEvent evt, CancellationToken ct = default) =>
            _channel.Writer.WriteAsync(evt, ct);

        public IAsyncEnumerable<INotificationEvent> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);
    }
}