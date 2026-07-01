namespace APDS.Models
{
    public enum NotificationType
    {
        APPROVAL,
        REJECTION,
        REVISION_REQUESTED,
        ASSIGNMENT,
        REMINDER,
        DELEGATION_REQUEST,  
        DELEGATION_ACCEPTED, 
        DELEGATION_REJECTED   
    }

    public class Notification
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public User User { get; set; }

        public NotificationType Type { get; set; }
        public string Message { get; set; }

        public int? RelatedEntityId { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}