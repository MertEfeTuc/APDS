using APDS.Models;

namespace APDS.Events
{
    public interface INotificationEvent
    {
        string RecipientUserId { get; }
        NotificationType Type { get; }
        string Message { get; }
        int? RelatedEntityId { get; }
    }

    public record ActivityStatusChangedEvent(
        string RecipientUserId,
        NotificationType Type,
        string Message,
        int? RelatedEntityId
    ) : INotificationEvent;
}