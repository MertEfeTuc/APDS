namespace APDS.Models
{
    public class ActivityVersion
    {
        public int Id { get; set; }

        public int ActivityId { get; set; }
        public Activity Activity { get; set; }

        public int VersionNumber { get; set; } // Aynı ActivityId için 1'den başlayıp artar

        public string Title { get; set; }
        public string Description { get; set; }
        public DateOnly ActivityDate { get; set; }
        public ActivityStatus Status { get; set; }

        public int ActivityTypeId { get; set; }

        public string? JournalName { get; set; }
        public string? FundingAgency { get; set; }
        public string? PatentNumber { get; set; }
        public string? ThesisNumber { get; set; }

        public string SnapshotReason { get; set; } = "EDIT"; // "EDIT" | "RESUBMIT"
        public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    }
}