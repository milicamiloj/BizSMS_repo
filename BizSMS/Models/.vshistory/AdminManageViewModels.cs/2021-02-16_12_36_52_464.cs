using BizSMS.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace BizSMS.Models
{
    #region Clients
    public class AdminManageClientsViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }

        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        public string Username { get; set; }

        [Required]
        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }

        [Required]
        [Display(Name = "MtsID", ResourceType = typeof(Resources.Resources))]
        public string MtsID { get; set; }

        [Required]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }

        [Display(Name = "Locked", ResourceType = typeof(Resources.Resources))]
        public bool IsCanceled { get; set; }
    }

    public class AdminCreateClientViewModel
    {
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "MtsID", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckMTSID", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string Mts_ID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "ContractID", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckContractID", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string ContractID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "ClientName", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckClientname", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string ClientName { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        //[EmailAddress(ErrorMessageResourceType = typeof(Resources.Resources),
        //      ErrorMessageResourceName = "InvalidEmail")]
        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckUsername", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "NoEmptySpaceOrSpecChars")]
        public string Username { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }

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

    public class AdminEditClientViewModel
    {
        [Required]
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Remote("CheckMTSID", "Validation", AdditionalFields = "InitialMtsId", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        [Display(Name = "MtsID", ResourceType = typeof(Resources.Resources))]
        public string Mts_ID { get; set; }

        //[Required(ErrorMessageResourceType = typeof(Resources.Resources),
        //     ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "ContractID", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckContractID", "Validation", AdditionalFields = "InitialContractId", HttpMethod = "POST",
           ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string ContractID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "ClientName", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckClientname", "Validation", AdditionalFields = "InitialClientName", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string ClientName { get; set; }

        //[EmailAddress(ErrorMessageResourceType = typeof(Resources.Resources),
        //      ErrorMessageResourceName = "InvalidEmail")]
        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        public string Username { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }
    }

    public class ClientContractsViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int ClientId { get; set; }

        [Display(Name = "ClientName", ResourceType = typeof(Resources.Resources))]
        public string ClientName { get; set; }
    }

    public class AddClientContractViewModel: ClientContractsViewModel
    {
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "ContractID", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckContractID", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string ContractId { get; set; }
    }

    public class EditClientContractViewModel: AddClientContractViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int ClientContractId { get; set; }
    }

    public class ClientUsersViewModel
    {
        [Required]
        [Display(Name = "UserID")]
        public string UserID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        public string Username { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }
    }

    public class EditClientUserViewModel
    {
        [Required]
        [Display(Name = "UserID")]
        public string UserID { get; set; }

        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckUsername", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string Username { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "PhoneNumberIsRequired")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }
    }

    public class CreateClientUserViewModel
    {
        [HiddenInput(DisplayValue = false)]
        [Required]
        public int ClientId { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckUsername", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
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

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "PhoneNumberIsRequired")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }
    }

    public class EditUserViewModel
    {
        [Required]
        [HiddenInput(DisplayValue = false)]
        public string UserID { get; set; }

        public int ClientID { get; set; }

        [Display(Name = "Username", ResourceType = typeof(Resources.Resources))]
        [Remote("CheckUsername", "Validation", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string Username { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "PhoneNumberIsRequired")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Display(Name = "PhoneNumber", ResourceType = typeof(Resources.Resources))]
        public string PhoneNumber { get; set; }
    }

    #endregion

    #region Alphanumerics
    public class AlphanumericViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int AlphanumericID { get; set; }

        [Display(Name = "Alphanumeric", ResourceType = typeof(Resources.Resources))]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [StringLength(11, ErrorMessageResourceType = typeof(Resources.Resources),
               ErrorMessageResourceName = "LengthValidation", MinimumLength = 3)]
        [Remote("CheckAlphanumeric", "Validation", AdditionalFields = "InitialAlphanumeric", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        [RegularExpression(@"^([ A-Za-z0-9_-]{3,11})$", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "AlphanumericRegExValidation")]
        public string Alphanumeric { get; set; }

        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }
    }

    public class AlphanumericCreateViewModel
    {
        [Display(Name = "Alphanumeric", ResourceType = typeof(Resources.Resources))]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [StringLength(11, ErrorMessageResourceType = typeof(Resources.Resources),
               ErrorMessageResourceName = "LengthValidation", MinimumLength = 3)]
        //[Remote("CheckAlphanumeric", "Validation", HttpMethod = "POST",
        //    ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        [RegularExpression(@"^([ A-Za-z0-9_-]{3,11})$", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "AlphanumericRegExValidation")]
        public string Alphanumeric { get; set; }

        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }
    }
    #endregion

    #region Groups
    public class AdminManageGroupsViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }

        [Display(Name = "ClientName", ResourceType = typeof(Resources.Resources))]
        public string ClientName { get; set; }

        //[Display(Name = "GroupName", ResourceType = typeof(Resources.Resources))]
        //public string Name { get; set; }

        [Display(Name = "TotalOfNumbers", ResourceType = typeof(Resources.Resources))]
        public int TotalOfNumbers { get; set; }
    }

    public class AdminCreateGroupViewModel
    {
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "ClientName", ResourceType = typeof(Resources.Resources))]
        public int ClientID { get; set; }
        public System.Web.Mvc.SelectList Clients { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "GroupName", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }
    }

    public class AdminEditGroupViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int GroupID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "ClientName", ResourceType = typeof(Resources.Resources))]
        public int ClientID { get; set; }
        public System.Web.Mvc.SelectList Clients { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
             ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "GroupName", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }
    }
    #endregion


    #region MessageCost
    public class EditMessageCostViewModel
    {
        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int MessageCostID { get; set; }

        [Display(Name = "NumberOfMessagesFrom", ResourceType = typeof(Resources.Resources))]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [OverlapingRange("NumberTypeID", "MessageCostID")]
        public int NumberOfMessagesFrom { get; set; }

        [Display(Name = "NumberOfMessagesTo", ResourceType = typeof(Resources.Resources))]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [NumberGreaterThen("NumberOfMessagesFrom", ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsNotGreaterThan")]
        [OverlapingRange("NumberTypeID", "MessageCostID")]
        public int NumberOfMessagesTo { get; set; }

        [Display(Name = "Price", ResourceType = typeof(Resources.Resources))]
        [DisplayFormat(DataFormatString = "{0:0.##}")]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [CheckPrice("NumberOfMessagesTo", "NumberTypeID", ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "PriceError")]
        public double Price { get; set; }

        [System.Web.Mvc.HiddenInput(DisplayValue = false)]
        public int NumberTypeID { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberType { get; set; }

        /// <summary>
        /// Determines whether the specified object is MessageCost object,
        /// and if it is compares NumberOfMessagesFrom, NumberOfMessagesTo and Price
        /// properties
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is MessageCostModel)
            {
                MessageCostModel mc = (MessageCostModel)obj;
                if (NumberOfMessagesFrom == mc.NumberOfMessagesFrom && NumberOfMessagesTo == mc.NumberOfMessagesTo && Price == mc.Price)
                {
                    return true;
                }
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class MessageCostListViewModel
    {
        public IEnumerable<MessageCostModel> MessageCosts { get; set; }
        public string NumberType { get; set; }
    } 

    public class CreateMessageCostViewModel
    {
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public int NumberTypeID { get; set; }

        public System.Web.Mvc.SelectList NumberTypes { get; set; }

        [Display(Name = "NumberOfMessagesFrom", ResourceType = typeof(Resources.Resources))]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [OverlapingRange("NumberTypeID", "")]
        public int NumberOfMessagesFrom { get; set; }

        [Display(Name = "NumberOfMessagesTo", ResourceType = typeof(Resources.Resources))]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [NumberGreaterThen("NumberOfMessagesFrom", ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsNotGreaterThan")]
        [OverlapingRange("NumberTypeID", "")]
        public int NumberOfMessagesTo { get; set; }

        [Display(Name = "Price", ResourceType = typeof(Resources.Resources))]
        [DisplayFormat(DataFormatString = "{0:0.##}")]
        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "IsRequired")]
        [CheckPrice("NumberOfMessagesTo", "NumberTypeID", ErrorMessageResourceType = typeof(Resources.Resources),
              ErrorMessageResourceName = "PriceError")]
        public double Price { get; set; }
    }
    #endregion

    #region Numbers
    public class AdminManageNumbersViewModel
    {
        public int NumberID { get; set; }

        [Display(Name = "Group", ResourceType = typeof(Resources.Resources))]
        public string Group { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberType { get; set; }

        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        public string Number { get; set; }

        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }

        [Display(Name = "DeniedReason", ResourceType = typeof(Resources.Resources))]
        public string DeniedReason { get; set; }

        [Display(Name = "SendAllowed", ResourceType = typeof(Resources.Resources))]
        public bool SendAllowed { get; set; }
    }

    public class AdminCreateNumberViewModel
    {
        public int ClientID { get; set; }
        public int GroupID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
             ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        [StringLength(12, ErrorMessageResourceType = typeof(Resources.Resources),
               ErrorMessageResourceName = "LengthValidation", MinimumLength = 8)]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Remote("NumberExist", "Validation", AdditionalFields = "ClientID", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string Number { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
             ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        [Remote("NumberTypeCantBeInGroup", "Validation", AdditionalFields = "GroupID", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "NumberTypeIsNotBelong")]
        public int NumberTypeID { get; set; }
        public System.Web.Mvc.SelectList NumberType { get; set; }
        
        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
             ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "SendAllowed", ResourceType = typeof(Resources.Resources))]
        public bool SendAllowed { get; set; }
    }

    public class AdminEditNumberViewModel
    {
        public int ClientID { get; set; }
        public int GroupID { get; set; }
        public int NumberID { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
             ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        [StringLength(12, ErrorMessageResourceType = typeof(Resources.Resources),
               ErrorMessageResourceName = "LengthValidation", MinimumLength = 8)]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessageResourceType = typeof(Resources.Resources),
            ErrorMessageResourceName = "PhoneNotValid")]
        [Remote("NumberExist", "Validation", AdditionalFields = "InitialNumber,ClientID", HttpMethod = "POST",
            ErrorMessageResourceType = typeof(Resources.Resources), ErrorMessageResourceName = "AlreadyExists")]
        public string Number { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
             ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public int NumberTypeID { get; set; }
        public System.Web.Mvc.SelectList NumberType { get; set; }

        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }

        [Required(ErrorMessageResourceType = typeof(Resources.Resources),
             ErrorMessageResourceName = "IsRequired")]
        [Display(Name = "SendAllowed", ResourceType = typeof(Resources.Resources))]
        public bool SendAllowed { get; set; }
    }
    #endregion

    #region UploadFile
    public class AdminUploadFileViewModel
    {
        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        public string Number { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberType { get; set; }

        public string Name { get; set; }
    }

    public class AdminConfirmUploadFileViewModel
    {
        public List<AdminUploadFileViewModel> Numbers { get; set; }
    }
    #endregion

    #region API
    public class TempImportData
    {
        public List<TempImportUpload> data { get; set; }
    }

    public class TempImportUpload
    {
        public string Number { get; set; }
        public string NumberType { get; set; }
        public string Name { get; set; }
    }

    public class NumbersViewModel
    {
        public int NumberID { get; set; }

        //[Display(Name = "Group", ResourceType = typeof(Resources.Resources))]
        //public string Group { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberType { get; set; }

        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        public string Number { get; set; }

        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }

        [Display(Name = "SendAllowed", ResourceType = typeof(Resources.Resources))]
        public string SendAllowed { get; set; }

        [Display(Name = "DeniedReason", ResourceType = typeof(Resources.Resources))]
        public string DeniedReason { get; set; }

        public string EditSection { get; set; }
    }

    public class NumbersListViewModel
    {
        public List<NumbersViewModel> data { get; set; }
    }

    public class ClientData
    {
        public string MTS_ID { get; set; }

        public string ClientName { get; set; }

        public string ContractID { get; set; }
    }

    public class ImportNumbers
    {
        public string MTS_ID { get; set; }

        public string ClientName { get; set; }

        public string ContractID { get; set; }

        public string PublicNR { get; set; }
    }
    #endregion

    public class DenySendingReason
    {
        [Display(Name = "Reason", ResourceType = typeof(Resources.Resources))]
        public string Reason { get; set; }

        public int NumberID { get; set; }
    }
}