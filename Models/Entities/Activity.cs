using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace APDS.Models
{
    public enum ActivityStatus
    {
        DRAFT,
        SUBMITTED,
        UNDER_REVIEW,
        APPROVED,
        REJECTED,
        REVISION_REQUESTED,
        RESUBMITTED
    }

   public class Activity
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        public DateOnly ActivityDate { get; set; }   // DateTime yerine

        public ActivityStatus Status { get; set; }

        // Foreign keys
        public string AcademicianId { get; set; }
        public User Academician { get; set; }

        public int ActivityTypeId { get; set; }
        public ActivityType ActivityType { get; set; }

        // ---- Tür-spesifik alanlar (nullable, sadece ilgili türde doldurulur) ----

        public string? JournalName { get; set; }      // Yayın
        public string? FundingAgency { get; set; }    // Proje
        public string? PatentNumber { get; set; }      // Patent
        public string? ThesisNumber { get; set; }        // Tez Danışmanlığı

        public string? PendingDelegationReviewerId { get; set; }  // devir teklifi bekleniyor
        public User? PendingDelegationReviewer { get; set; }

        public string? DelegatedReviewerId { get; set; }  // kabul edilince buraya taşınır
        public User? DelegatedReviewer { get; set; }

        public DateTime LastStatusChangeDate { get; set; } = DateTime.UtcNow;
        public bool OverdueNotificationSent { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}