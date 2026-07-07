using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class ProfileEditViewModel
    {
        [MaxLength(100)]
        public string? Title { get; set; }

        [MaxLength(500)]
        public string? Bio { get; set; }

        [RegularExpression(@"^\d{4}-\d{4}-\d{4}-\d{3}[0-9X]$",
            ErrorMessage = "Geçerli bir ORCID ID giriniz (örn. 0000-0001-2345-6789).")]
        public string? OrcidId { get; set; }
    }
}