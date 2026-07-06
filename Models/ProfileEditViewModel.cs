using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class ProfileEditViewModel
    {
        [MaxLength(100)]
        public string? Title { get; set; }

        [MaxLength(500)]
        public string? Bio { get; set; }
    }
}