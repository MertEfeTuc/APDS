using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace APDS.Models.Admin
{
    public class ReviewerAssignmentViewModel
    {
        public int? Id { get; set; }   // null = yeni atama, dolu = düzenleme

        [Required(ErrorMessage = "Akademisyen seçin.")]
        public string AcademicianId { get; set; }

        [Required(ErrorMessage = "Reviewer seçin.")]
        public string ReviewerId { get; set; }

        public List<SelectListItem> Academicians { get; set; } = new();
        public List<SelectListItem> Reviewers { get; set; } = new();
    }
}