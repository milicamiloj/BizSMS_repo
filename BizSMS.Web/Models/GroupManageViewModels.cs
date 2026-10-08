#nullable disable
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BizSMS.Web.Models
{
    // Portovano iz BizSMS/Models/GroupManageViewModels.cs + ClientManageViewModels.cs.
    // ResourceType lokalizacija zamenjena plain-string porukama (srpski) do koraka lokalizacije.

    public class UserManageGroupsViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }

        [HiddenInput(DisplayValue = false)]
        public bool isDefault { get; set; }

        [Display(Name = "Ime grupe")]
        public string Name { get; set; }

        [Display(Name = "Ukupno brojeva")]
        public int TotalOfNumbers { get; set; }
    }

    public class UserCreateGroupViewModel
    {
        [Required(ErrorMessage = "Obavezno polje")]
        [Display(Name = "Naziv grupe")]
        [StringLength(30, MinimumLength = 5, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera")]
        public string Name { get; set; }
    }

    public class UserEditGroupViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }

        [Required(ErrorMessage = "Obavezno polje")]
        [Display(Name = "Naziv grupe")]
        public string Name { get; set; }
    }

    public class UserManageNumbersViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int NumberID { get; set; }

        [HiddenInput(DisplayValue = false)]
        public int? GroupID { get; set; }

        [Display(Name = "Grupa")]
        public string Group { get; set; }

        [Display(Name = "Tip broja")]
        public string NumberType { get; set; }

        [Display(Name = "Broj")]
        public string Number { get; set; }

        [Display(Name = "Naziv")]
        public string Name { get; set; }
    }

    public class UserSelectListViewModel
    {
        public int NumberID { get; set; }
        public string NameNumber { get; set; }
    }

    public class UserAddNumberViewModel
    {
        [HiddenInput(DisplayValue = false)]
        [Display(Name = "Izaberite broj sa liste")]
        [Required(ErrorMessage = "Obavezno polje")]
        public int[] NumberID { get; set; }

        public MultiSelectList Numbers { get; set; }

        [HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }
    }

    public class UserAddOneNumberViewModel
    {
        public int GroupID { get; set; }

        [Display(Name = "Broj")]
        [Required(ErrorMessage = "Unesite broj u formatu 06XXXXXXXX")]
        [RegularExpression(@"^(06\d{7,8})", ErrorMessage = "Broj nije validan")]
        public string Number { get; set; }

        [Display(Name = "Naziv")]
        [Required(ErrorMessage = "Obavezno polje")]
        [StringLength(30, ErrorMessage = "Maksimalan broj karaktera je {1}")]
        public string NameNumber { get; set; }

        public string NumberType { get; set; }
    }

    public class UserEditNumberViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int NumberID { get; set; }

        [HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }

        [Display(Name = "Broj")]
        public string Number { get; set; }

        [Required(ErrorMessage = "Obavezno polje")]
        [Display(Name = "Naziv")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera")]
        public string Name { get; set; }
    }

    public class ClientUploadFileViewModel
    {
        [Display(Name = "Broj")]
        public string Number { get; set; }

        [Display(Name = "Tip broja")]
        public string NumberType { get; set; }

        public string Name { get; set; }
    }

    public class ClientConfirmUploadFileViewModel
    {
        public List<ClientUploadFileViewModel> Numbers { get; set; }
    }
}
