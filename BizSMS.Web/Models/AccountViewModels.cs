#nullable disable
using System.ComponentModel.DataAnnotations;

namespace BizSMS.Web.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Obavezno polje")]
        [Display(Name = "Korisničko ime")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Obavezno polje")]
        [DataType(DataType.Password)]
        [Display(Name = "Lozinka")]
        public string Password { get; set; }

        [Display(Name = "Zapamti me")]
        public bool RememberMe { get; set; }
    }

    public class VerifyPhoneNumberViewModel
    {
        [Required(ErrorMessage = "Obavezno polje")]
        [Display(Name = "Verifikacioni kod")]
        public string Code { get; set; }

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }

        // Vreme slanja koda — koristi se za odbrojavanje/isticanje (2 min) u view-u.
        public DateTime? PhoneCodeSentAt { get; set; }
    }
}
