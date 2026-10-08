#nullable disable
using System.ComponentModel.DataAnnotations;

namespace BizSMS.Web.Models
{
    // Zamena za legacy BizSMS.Models.ChangePasswordViewModel (Models/ManageViewModels.cs).
    // ResourceType/ErrorMessageResource* zamenjeni plain srpskim stringovima.
    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Trenutna lozinka")]
        public string OldPassword { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "{0} mora da sadrži najmanje {2} karaktera.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nova lozinka")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Potvrdi lozinku")]
        [Compare("NewPassword", ErrorMessage = "Lozinke se ne podudaraju")]
        public string ConfirmPassword { get; set; }
    }
}
