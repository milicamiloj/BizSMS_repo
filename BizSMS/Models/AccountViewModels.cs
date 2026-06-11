using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace BizSMS.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessageResourceName = "IsRequired", 
            ErrorMessageResourceType = typeof(Resources.Resources))]
        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        //[EmailAddress(ErrorMessageResourceName = "InvalidEmail",
        //    ErrorMessageResourceType = typeof(Resources.Resources))]
        public string Username { get; set; }

        [Required(ErrorMessageResourceName = "IsRequired", 
            ErrorMessageResourceType = typeof(Resources.Resources))]
        [DataType(DataType.Password)]
        [Display(Name = "Password", ResourceType = typeof(Resources.Resources))]
        public string Password { get; set; }

        [Display(Name = "RememberMe", ResourceType = typeof(Resources.Resources))]
        public bool RememberMe { get; set; }
    }

    public class SendCodeViewModel
    {
        //[Required]
        public string SelectedProvider { get; set; }

        public List<SelectListItem> Providers { get; set; }

        public string ReturnUrl { get; set; }

        public bool RememberMe { get; set; }
    }
    public class TwoFactorModel
    {
        [Required]
        public string Provider { get; set; }

        [Required]
        public string Code { get; set; }

        public string ReturnUrl { get; set; }

        [Display(Name = "Remember this browser?")]
        public bool RememberBrowser { get; set; }
        public bool RememberMe { get; set; }
    }

    public class ChooseProviderModel
    {
         //public List<string> Providers { get; set; }
        public List<SelectListItem> Providers { get; set; }

        public string SelectedProvider { get; set; } //ChosenProvider { get; set; }
        public string ReturnUrl { get; set; }

        public bool RememberMe { get; set; }
    }
    public class RegisterViewModel
    {
        [Required]
        public int ClientID { get; set; }
        public System.Web.Mvc.SelectList Clients { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(256, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        public string Username { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class ResetPasswordViewModel
    {
        public int ClientID { get; set; }

        public string UserID { get; set; }
        
        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        public string Username { get; set; }

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
    }

    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
    //public class VerifyCodeViewModel
    //{
    //    [Required]
    //    public string Provider { get; set; }

    //    [Required]
    //    public string Code { get; set; }

    //    public string ReturnUrl { get; set; }

    //    [Display(Name = "Remember this browser?")]
    //    public bool RememberBrowser { get; set; }
    //    public bool RememberMe { get; set; }

    //}
}
