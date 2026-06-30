using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class ActivityFormViewModel
    {
        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "Tarih gerekli.")]
        [DataType(DataType.Date)]
        [Display(Name = "Tarih")]
        public DateOnly ActivityDate { get; set; }

        [Required]
        public int ActivityTypeId { get; set; }

        public string? JournalName { get; set; }
        public string? FundingAgency { get; set; }
        public string? PatentNumber { get; set; }
        public string? ThesisNumber { get; set; }

        public List<ActivityType> AllActivityTypes { get; set; } = new();
    }
}