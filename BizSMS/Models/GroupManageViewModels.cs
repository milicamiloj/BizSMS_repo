using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace BizSMS.Models
{
    public class UserManageGroupsViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }

        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public bool isDefault { get; set; }

        [Display(Name = "GroupName", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }

        [Display(Name = "TotalOfNumbers", ResourceType = typeof(Resources.Resources))]
        public int TotalOfNumbers { get; set; }
    }

    public class UserCreateGroupViewModel
    {
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "GroupName", ResourceType = typeof(Resources.Resources))]
        [StringLength(30, ErrorMessageResourceName = "LengthValidation", 
            ErrorMessageResourceType = typeof(Resources.Resources), MinimumLength = 5)]
        public string Name { get; set; }
    }

    public class UserEditGroupViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
             ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "GroupName", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }
    }

    public class UserManageNumbersViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int NumberID { get; set; }

        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int? GroupID { get; set; }

        [Display(Name = "Group", ResourceType = typeof(Resources.Resources))]
        public string Group { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberType { get; set; }

        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        public string Number { get; set; }

        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }
    }

    public class UserSelectListViewModel
    {
        public int NumberID { get; set; }

        public string NameNumber { get; set; }
    }

    public class UserAddNumberViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        [Display(Name = "ChooseNumberFromList", ResourceType = typeof(Resources.Resources))]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "IsRequired")]
        public int[] NumberID { get; set; }
        public System.Web.Mvc.MultiSelectList Numbers { get; set; }

        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }
    }

    public class UserAddOneNumberViewModel
    {
        public int GroupID { get; set; }
        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "RequiredNumberFormat")]
        [RegularExpression(@"^(06\d{7,8})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Remote("NumberExistInGroup", "Validation", AdditionalFields = "GroupID", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExistsInGroup")]
        public string Number { get; set; }

        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        [Required (ErrorMessageResourceType = typeof(Resources.Resources), 
            ErrorMessageResourceName = "Required")]
        [StringLength(30, ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "MaxNumberOfCharacters")]
        [Remote("LimitNumberOfCharacters", "Validation", HttpMethod = "POST", ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "MaxNumberOfCharacters")]
        public string NameNumber { get; set; }

        public string NumberType { get; set; }
    }

    public class UserEditNumberViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int NumberID { get; set; }

        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }

        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        public string Number { get; set; }

        [Required(ErrorMessageResourceName = "IsRequired", ErrorMessageResourceType = typeof(Resources.Resources))]
        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        [StringLength(30, ErrorMessageResourceName = "LengthValidation", 
            ErrorMessageResourceType = typeof(Resources.Resources), MinimumLength = 3)]
        public string Name { get; set; }
    }
}