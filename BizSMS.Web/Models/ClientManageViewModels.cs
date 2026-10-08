#nullable disable
using System.ComponentModel.DataAnnotations;

namespace BizSMS.Web.Models
{
    // Zamena za legacy BizSMS.Models.ClientManage* view-modele.
    // ResourceType/[Remote] zamenjeni plain srpskim stringovima (server-side provere pokrivaju).
    public class ClientManageUsersViewModel
    {
        [Required]
        public string UserID { get; set; }

        [Required(ErrorMessage = "{0} je obavezno polje")]
        [Display(Name = "E-mail")]
        [EmailAddress(ErrorMessage = "Polje {0} nije validna e-mail adresa")]
        public string Email { get; set; }

        [Required(ErrorMessage = "{0} je obavezno polje")]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }
    }

    public class ClientCreateUserViewModel
    {
        [Required(ErrorMessage = "Email je obavezno polje")]
        [EmailAddress(ErrorMessage = "Polje {0} nije validna e-mail adresa")]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [Required(ErrorMessage = "{0} je obavezno polje")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "{0} mora da sadrži najmanje {2} karaktera.")]
        [DataType(DataType.Password)]
        [Display(Name = "Lozinka")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Potvrdi lozinku")]
        [Compare("Password", ErrorMessage = "Lozinke se ne podudaraju")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Broj telefona je obavezno polje")]
        [RegularExpression(@"^(06\d{8,10})", ErrorMessage = "{0} polje ne predstavlja validan broj telefona")]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }
    }

    public class ClientEditUserViewModel
    {
        [Required]
        public string UserID { get; set; }

        [Display(Name = "Korisničko ime")]
        public string Username { get; set; }

        [Display(Name = "E-mail")]
        [EmailAddress(ErrorMessage = "Polje {0} nije validna e-mail adresa")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Broj telefona je obavezno polje")]
        [RegularExpression(@"^(06\d{8,10})", ErrorMessage = "{0} polje ne predstavlja validan broj telefona")]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }
    }
}
