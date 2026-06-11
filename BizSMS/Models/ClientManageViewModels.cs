using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace BizSMS.Models
{
    //Manage users

    public class ClientTempImportData
    {
        public List<ClientTempImportUpload> data { get; set; }
    }

    public class ClientTempImportUpload
    {
        public string Number { get; set; }
        public string NumberType { get; set; }
        public string Name { get; set; }
    }

    public class ClientUploadFileViewModel
    {
        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        public string Number { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberType { get; set; }

        public string Name { get; set; }
    }

    public class ClientConfirmUploadFileViewModel
    {
        public List<ClientUploadFileViewModel> Numbers { get; set; }
    }
    public class ClientManageUsersViewModel
    {
        [Required]
        [Display(Name = "UserID")]
        public string UserID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "Email", ResourceType = typeof(Resources.Resources))]
        [EmailAddress(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "InvalidEmail")]
        public string Email { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }
    }

    public class ClientEditUserViewModel
    {
        [Required]
        [Display(Name = "UserID")]
        public string UserID { get; set; }

        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckUsername", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string Username { get; set; }

        [Display(Name = "Email", ResourceType = typeof(Resources.Resources))]
        [EmailAddress(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "InvalidEmail")]
        public string Email { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "PhoneNumberIsRequired")]
        [RegularExpression(@"^(06\d{8,10})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }
    }

    public class ClientCreateUserViewModel
    {
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "EmailIsRequired")]
        [EmailAddress(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "InvalidEmail")]
        [Display(Name = "Email", ResourceType = typeof(Resources.Resources))]
        //[Remote("CheckEmail", "Validation", HttpMethod = "POST",
        //    ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string Email { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [StringLength(100, ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "LengthValidation", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password", ResourceType = typeof(Resources.Resources))]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "ConfirmPassword", ResourceType = typeof(Resources.Resources))]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "PasswordsDontMatch")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "PhoneNumberIsRequired")]
        [RegularExpression(@"^(06\d{8,10})",ErrorMessageResourceType = typeof(Resources.Resources), 
            ErrorMessageResourceName = "PhoneNotValid")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }
    }
}