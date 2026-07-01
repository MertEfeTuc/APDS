namespace APDS.Models
{
    public class ActivityAttachment
    {
        public int Id { get; set; }

        public int ActivityId { get; set; }
        public Activity Activity { get; set; }

        public string OriginalFileName { get; set; }
        public string StoredFileName { get; set; } // disk üzerindeki gerçek dosya adı (GUID bazlı)
        public string ContentType { get; set; }
        public long FileSizeBytes { get; set; }

        public string UploadedByUserId { get; set; }
        public User UploadedBy { get; set; }

        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    }
}