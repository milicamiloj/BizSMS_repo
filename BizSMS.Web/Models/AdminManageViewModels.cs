#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BizSMS.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace BizSMS.Web.Models
{
    // Zamena za legacy BizSMS.Models.AdminManageViewModels (§8.4).
    // Resources.Resources.* -> plain srpski stringovi; [Remote] izostavljen (server-side provere pokrivaju).

    #region Clients
    public class AdminManageClientsViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }

        [Display(Name = "Korisničko ime")]
        public string Username { get; set; }

        [Required]
        [Display(Name = "Naziv")]
        public string Name { get; set; }

        [Required]
        [Display(Name = "MTS ID")]
        public string MtsID { get; set; }

        [Required]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Zaključan")]
        public bool IsCanceled { get; set; }
    }

    public class AdminCreateClientViewModel
    {
        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "MTS ID")]
        public string Mts_ID { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Broj ugovora")]
        public string ContractID { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Naziv klijenta")]
        public string ClientName { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Korisničko ime")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Bez razmaka i specijalnih karaktera.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessage = "Broj telefona nije validan.")]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [StringLength(100, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Lozinka")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Potvrda lozinke")]
        [Compare("Password", ErrorMessage = "Lozinke se ne poklapaju.")]
        public string ConfirmPassword { get; set; }
    }

    public class AdminEditClientViewModel
    {
        [Required]
        [HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "MTS ID")]
        public string Mts_ID { get; set; }

        [Display(Name = "Broj ugovora")]
        public string ContractID { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Naziv klijenta")]
        public string ClientName { get; set; }

        [Display(Name = "Korisničko ime")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessage = "Broj telefona nije validan.")]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }
    }

    public class ClientContractsViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int ClientId { get; set; }

        [Display(Name = "Naziv klijenta")]
        public string ClientName { get; set; }
    }

    public class AddClientContractViewModel : ClientContractsViewModel
    {
        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Broj ugovora")]
        public string ContractId { get; set; }
    }

    public class EditClientContractViewModel : AddClientContractViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int ClientContractId { get; set; }
    }

    public class ClientUsersViewModel
    {
        [Required]
        [Display(Name = "UserID")]
        public string UserID { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Korisničko ime")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }
    }

    public class CreateClientUserViewModel
    {
        [HiddenInput(DisplayValue = false)]
        [Required]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Korisničko ime")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [StringLength(100, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Lozinka")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Potvrda lozinke")]
        [Compare("Password", ErrorMessage = "Lozinke se ne poklapaju.")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Broj telefona je obavezan.")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessage = "Broj telefona nije validan.")]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }
    }

    public class EditUserViewModel
    {
        [Required]
        [HiddenInput(DisplayValue = false)]
        public string UserID { get; set; }

        public int ClientID { get; set; }

        [Display(Name = "Korisničko ime")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Broj telefona je obavezan.")]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessage = "Broj telefona nije validan.")]
        [Display(Name = "Broj telefona")]
        public string PhoneNumber { get; set; }
    }

    // Zamena za legacy BizSMS.Models.ResetPasswordViewModel (admin resetuje lozinku korisnika klijenta).
    public class ResetPasswordViewModel
    {
        [HiddenInput(DisplayValue = false)]
        [Required]
        public string UserID { get; set; }

        [HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }

        [Display(Name = "Korisničko ime")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [StringLength(100, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Nova lozinka")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Potvrda lozinke")]
        [Compare("Password", ErrorMessage = "Lozinke se ne poklapaju.")]
        public string ConfirmPassword { get; set; }
    }
    #endregion

    #region Alphanumerics
    public class AlphanumericViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int AlphanumericID { get; set; }

        [Display(Name = "Alfanumerik")]
        [Required(ErrorMessage = "Obavezno polje.")]
        [StringLength(11, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera.", MinimumLength = 3)]
        [RegularExpression(@"^([ A-Za-z0-9_-]{3,11})$", ErrorMessage = "Dozvoljena su slova, brojevi, razmak, _ i - (3-11 karaktera).")]
        public string Alphanumeric { get; set; }

        [HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }
    }

    public class AlphanumericCreateViewModel
    {
        [Display(Name = "Alfanumerik")]
        [Required(ErrorMessage = "Obavezno polje.")]
        [StringLength(11, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera.", MinimumLength = 3)]
        [RegularExpression(@"^([ A-Za-z0-9_-]{3,11})$", ErrorMessage = "Dozvoljena su slova, brojevi, razmak, _ i - (3-11 karaktera).")]
        public string Alphanumeric { get; set; }

        [HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }
    }
    #endregion

    #region Groups
    public class AdminManageGroupsViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int ClientID { get; set; }

        [Display(Name = "Naziv klijenta")]
        public string ClientName { get; set; }

        [Display(Name = "Ukupno brojeva")]
        public int TotalOfNumbers { get; set; }
    }
    #endregion

    #region MessageCost
    public class EditMessageCostViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public int MessageCostID { get; set; }

        [Display(Name = "Broj poruka od")]
        [Required(ErrorMessage = "Obavezno polje.")]
        [OverlapingRange("NumberTypeID", "MessageCostID")]
        public int NumberOfMessagesFrom { get; set; }

        [Display(Name = "Broj poruka do")]
        [Required(ErrorMessage = "Obavezno polje.")]
        [NumberGreaterThen("NumberOfMessagesFrom", ErrorMessage = "{0} mora biti veći od {1}.")]
        [OverlapingRange("NumberTypeID", "MessageCostID")]
        public int NumberOfMessagesTo { get; set; }

        [Display(Name = "Cena")]
        [DisplayFormat(DataFormatString = "{0:0.##}")]
        [Required(ErrorMessage = "Obavezno polje.")]
        [CheckPrice("NumberOfMessagesTo", "NumberTypeID", ErrorMessage = "Cena ne može biti veća od cene za manji opseg.")]
        public double Price { get; set; }

        [HiddenInput(DisplayValue = false)]
        public int NumberTypeID { get; set; }

        [Display(Name = "Tip broja")]
        public string NumberType { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is MessageCostModel mc)
            {
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
        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Tip broja")]
        public int NumberTypeID { get; set; }

        public SelectList NumberTypes { get; set; }

        [Display(Name = "Broj poruka od")]
        [Required(ErrorMessage = "Obavezno polje.")]
        [OverlapingRange("NumberTypeID", "")]
        public int NumberOfMessagesFrom { get; set; }

        [Display(Name = "Broj poruka do")]
        [Required(ErrorMessage = "Obavezno polje.")]
        [NumberGreaterThen("NumberOfMessagesFrom", ErrorMessage = "{0} mora biti veći od {1}.")]
        [OverlapingRange("NumberTypeID", "")]
        public int NumberOfMessagesTo { get; set; }

        [Display(Name = "Cena")]
        [DisplayFormat(DataFormatString = "{0:0.##}")]
        [Required(ErrorMessage = "Obavezno polje.")]
        [CheckPrice("NumberOfMessagesTo", "NumberTypeID", ErrorMessage = "Cena ne može biti veća od cene za manji opseg.")]
        public double Price { get; set; }
    }
    #endregion

    #region Numbers
    public class AdminManageNumbersViewModel
    {
        public int NumberID { get; set; }

        [Display(Name = "Grupa")]
        public string Group { get; set; }

        [Display(Name = "Tip broja")]
        public string NumberType { get; set; }

        [Display(Name = "Broj")]
        public string Number { get; set; }

        [Display(Name = "Naziv")]
        public string Name { get; set; }

        [Display(Name = "Razlog zabrane")]
        public string DeniedReason { get; set; }

        [Display(Name = "Dozvoljeno slanje")]
        public bool SendAllowed { get; set; }
    }

    public class AdminCreateNumberViewModel
    {
        public int ClientID { get; set; }
        public int GroupID { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Broj")]
        [StringLength(12, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera.", MinimumLength = 8)]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessage = "Broj telefona nije validan.")]
        public string Number { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Tip broja")]
        public int NumberTypeID { get; set; }

        public SelectList NumberType { get; set; }

        [Display(Name = "Naziv")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Dozvoljeno slanje")]
        public bool SendAllowed { get; set; }
    }

    public class AdminEditNumberViewModel
    {
        public int ClientID { get; set; }
        public int GroupID { get; set; }
        public int NumberID { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Broj")]
        [StringLength(12, ErrorMessage = "Dužina mora biti između {2} i {1} karaktera.", MinimumLength = 8)]
        [RegularExpression(@"^(06\d{7,10})", ErrorMessage = "Broj telefona nije validan.")]
        public string Number { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Tip broja")]
        public int NumberTypeID { get; set; }

        public SelectList NumberType { get; set; }

        [Display(Name = "Naziv")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Obavezno polje.")]
        [Display(Name = "Dozvoljeno slanje")]
        public bool SendAllowed { get; set; }
    }
    #endregion

    #region API
    public class NumbersViewModel
    {
        public int NumberID { get; set; }

        [Display(Name = "Tip broja")]
        public string NumberType { get; set; }

        [Display(Name = "Broj")]
        public string Number { get; set; }

        [Display(Name = "Naziv")]
        public string Name { get; set; }

        [Display(Name = "Dozvoljeno slanje")]
        public string SendAllowed { get; set; }

        [Display(Name = "Razlog zabrane")]
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

    public class DenySendingReason
    {
        [Display(Name = "Razlog")]
        public string Reason { get; set; }

        public int NumberID { get; set; }
    }
    #endregion

    #region Custom validacioni atributi (§8.4)
    // Zamena za legacy BizSMS.Attributes.CompareAttributes.
    // DbContext se u Core validaciji dobija preko validationContext.GetService (DI scope zahteva),
    // umesto legacy `new ApplicationDbContext()`.

    public class NumberGreaterThenAttribute : ValidationAttribute
    {
        private readonly string _otherProperty;

        public NumberGreaterThenAttribute(string otherProperty) : base("{0} mora biti veći od {1}.")
        {
            _otherProperty = otherProperty;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var otherProperty = validationContext.ObjectInstance.GetType().GetProperty(_otherProperty);
            var otherValue = otherProperty.GetValue(validationContext.ObjectInstance, null);
            var thisNumberValue = Convert.ToInt32(value);
            var otherNumberValue = Convert.ToInt32(otherValue);

            if (thisNumberValue > otherNumberValue)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }
    }

    public class OverlapingRangeAttribute : ValidationAttribute
    {
        private readonly string _key;
        private readonly string _otherProperty;

        public OverlapingRangeAttribute(string otherProperty, string key) : base("Opsezi se ne smeju preklapati sa postojećim.")
        {
            _otherProperty = otherProperty;
            _key = key;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var otherProperty = validationContext.ObjectInstance.GetType().GetProperty(_otherProperty);
            var otherValue = otherProperty.GetValue(validationContext.ObjectInstance, null);
            int messageCostId = 0;

            if (!string.IsNullOrEmpty(_key))
            {
                var keyProperty = validationContext.ObjectInstance.GetType().GetProperty(_key);
                var keyValue = keyProperty.GetValue(validationContext.ObjectInstance, null);
                messageCostId = Convert.ToInt32(keyValue);
            }

            var numberTypeValue = Convert.ToInt32(otherValue);
            var rangeValue = Convert.ToInt32(value);

            var db = (ApplicationDbContext)validationContext.GetService(typeof(ApplicationDbContext));
            var overlap = db.MessageCost
                .Where(mc =>
                    mc.NumberOfMessagesFrom <= rangeValue && mc.NumberOfMessagesTo >= rangeValue &&
                    mc.NumberTypeID == numberTypeValue &&
                    mc.MessageCostID != messageCostId &&
                    mc.EndDate == null)
                .FirstOrDefault();

            if (overlap != null)
            {
                return new ValidationResult(ErrorMessage ?? "Opsezi se ne smeju preklapati sa postojećim.");
            }

            return ValidationResult.Success;
        }
    }

    public class CheckPriceAttribute : ValidationAttribute
    {
        private readonly string _numberOfMessagesTo;
        private readonly string _numberTypeID;

        public CheckPriceAttribute(string numberOfMessagesTo, string numberTypeID) : base("Cena ne može biti veća od cene za manji opseg.")
        {
            _numberOfMessagesTo = numberOfMessagesTo;
            _numberTypeID = numberTypeID;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var numberOfMessagesToProperty = validationContext.ObjectInstance.GetType().GetProperty(_numberOfMessagesTo);
            var numberOfMessagesToValue = numberOfMessagesToProperty.GetValue(validationContext.ObjectInstance, null);
            var numberTypeIDProperty = validationContext.ObjectInstance.GetType().GetProperty(_numberTypeID);
            var numberTypeIDValue = numberTypeIDProperty.GetValue(validationContext.ObjectInstance, null);

            var numberOfMessagesTo = Convert.ToInt32(numberOfMessagesToValue);
            var numberTypeID = Convert.ToInt32(numberTypeIDValue);
            var price = Convert.ToDouble(value);

            var db = (ApplicationDbContext)validationContext.GetService(typeof(ApplicationDbContext));
            var testPrice = db.MessageCost
                .Where(mc =>
                    mc.NumberOfMessagesTo < numberOfMessagesTo &&
                    mc.NumberTypeID == numberTypeID &&
                    mc.Price <= price &&
                    mc.EndDate == null)
                .FirstOrDefault();

            if (testPrice != null)
            {
                return new ValidationResult(ErrorMessage ?? "Cena ne može biti veća od cene za manji opseg.");
            }

            return ValidationResult.Success;
        }
    }
    #endregion
}
