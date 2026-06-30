using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class ActivityTypeFormViewModel
    {
        public int? Id { get; set; }   // null = Create, dolu = Edit

        [Required(ErrorMessage = "Kategori gerekli.")]
        [Display(Name = "Kategori")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Faaliyet adı gerekli.")]
        [Display(Name = "Faaliyet Adı")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Puan gerekli.")]
        [Range(0, 1000, ErrorMessage = "Puan 0-1000 arasında olmalı.")]
        [Display(Name = "Puan")]
        public int Score { get; set; }

        // Mevcut kategorileri dropdown'da önerebilmek için
        public List<string> ExistingCategories { get; set; } = new();
    }
}