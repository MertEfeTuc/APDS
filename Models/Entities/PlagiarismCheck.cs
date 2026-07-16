using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public enum PlagiarismCheckStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    public class PlagiarismCheck
    {
        public int Id { get; set; }

        public int ActivityId { get; set; }
        public Activity Activity { get; set; }

        public PlagiarismCheckStatus Status { get; set; } = PlagiarismCheckStatus.Pending;

        public double? SimilarityScore { get; set; }   // 0.0 - 1.0, sonuç gelene kadar null

        public string? FoundSourcesJson { get; set; }   // Gemini grounding sonucu (serialize edilmiş liste)

        public DateTime? CheckedDate { get; set; }       // kontrol tamamlandığında set edilir

        public bool ReviewerFlagged { get; set; } = false;   // reviewer elle "kopya" işaretlerse

        public string? ErrorMessage { get; set; }   // Status=Failed olduğunda hata detayı

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}