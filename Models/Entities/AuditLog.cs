namespace APDS.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string Operation { get; set; }
        public string PerformedBy { get; set; }
        public DateTime PerformedDate { get; set; } = DateTime.UtcNow;
    }
}