using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class AdminCreateUserViewModel
    {
        [Required(ErrorMessage = "Kullanıcı adı gerekli.")]
        [Display(Name = "Kullanıcı Adı")]
        public string Username { get; set; }

        [Required(ErrorMessage = "E-posta gerekli.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Şifre gerekli.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Kullanıcı türü seçmelisiniz.")]
        [Display(Name = "Kullanıcı Türü")]
        public string SelectedRole { get; set; }
        [Display(Name = "Bölüm")]
        public int? DepartmentId { get; set; }

        public List<Department> AllDepartments { get; set; } = new();
    }
}