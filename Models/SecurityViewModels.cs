using System.ComponentModel.DataAnnotations;

namespace APDS.Models
{
    public class TwoFactorLoginViewModel
    {
        [Required(ErrorMessage = "Doğrulama kodu zorunludur.")]
        [Display(Name = "Doğrulama Kodu")]
        public string Code { get; set; } = string.Empty;

        public bool RememberMe { get; set; }

        [Display(Name = "Bu cihazı hatırla")]
        public bool RememberMachine { get; set; }
    }

    public class RecoveryCodeLoginViewModel
    {
        [Required(ErrorMessage = "Kurtarma kodu zorunludur.")]
        [Display(Name = "Kurtarma Kodu")]
        public string RecoveryCode { get; set; } = string.Empty;
    }

    public class EnableAuthenticatorViewModel
    {
        public string SharedKey { get; set; } = string.Empty;
        public string AuthenticatorUri { get; set; } = string.Empty;

        [Required(ErrorMessage = "Doğrulama kodu zorunludur.")]
        [Display(Name = "Doğrulama Kodu")]
        public string? Code { get; set; }
    }

    public class SecurityIndexViewModel
    {
        public bool TwoFactorEnabled { get; set; }
        public int RecoveryCodesLeft { get; set; }
    }
}