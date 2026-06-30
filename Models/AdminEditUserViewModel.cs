using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class AdminEditUserViewModel
    {
        [Required]
        public string Id { get; set; }

        [Required(ErrorMessage = "Kullanıcı adı gerekli.")]
        [Display(Name = "Kullanıcı Adı")]
        public string Username { get; set; }

        [Required(ErrorMessage = "E-posta gerekli.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Yeni Şifre (boş bırakılırsa değişmez)")]
        public string? NewPassword { get; set; }

        // Sadece görüntülemek için, formdan değiştirilemez
        public string CurrentRole { get; set; }
    }
}