using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class Review
    {
        public int Id { get; set; }

        [Required]
        public int ActivityId { get; set; }
        public Activity Activity { get; set; }

        [Required]
        public string ReviewerId { get; set; }
        public User Reviewer { get; set; }

        public ActivityStatus Decision { get; set; }

        public string? Comment { get; set; }

        public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    }
}