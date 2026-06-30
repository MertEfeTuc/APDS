using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class DepartmentFormViewModel
    {
        public int? Id { get; set; }   // null = Create, dolu = Edit

        [Required(ErrorMessage = "Bölüm adı gerekli.")]
        [Display(Name = "Bölüm Adı")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Fakülte seçmelisiniz.")]
        [Display(Name = "Fakülte")]
        public int FacultyId { get; set; }

        public List<Faculty> AllFaculties { get; set; } = new();
    }

    public class FacultyFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Fakülte adı gerekli.")]
        [Display(Name = "Fakülte Adı")]
        public string Name { get; set; }
    }
}