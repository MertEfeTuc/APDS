using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class ActivityEditViewModel : ActivityFormViewModel
    {
        [Required]
        public int Id { get; set; }
    }
}