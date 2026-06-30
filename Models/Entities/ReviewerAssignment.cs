using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class ReviewerAssignment
    {
        public int Id { get; set; }

        [Required]
        public string AcademicianId { get; set; }
        public User Academician { get; set; }

        [Required]
        public string ReviewerId { get; set; }
        public User Reviewer { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
    }
}