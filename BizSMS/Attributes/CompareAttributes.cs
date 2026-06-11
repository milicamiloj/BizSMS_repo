using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Globalization;
using System.Web.Mvc;
using System.ComponentModel;
using BizSMS.Models;

namespace BizSMS.Attributes
{
    public class DateGreaterThenAttribute : ValidationAttribute
    {
        private readonly string _otherProperty;
        private const string _defaultErrorMessage = "{2} must be greater than {0}";

        public DateGreaterThenAttribute(string otherProperty) : base(_defaultErrorMessage)
        {
            _otherProperty = otherProperty;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var otherProperty = validationContext.ObjectInstance.GetType().GetProperty(_otherProperty);
            var otherValue = otherProperty.GetValue(validationContext.ObjectInstance, null);
            var thisDateValue = Convert.ToDateTime(value);
            var otherDateValue = Convert.ToDateTime(otherValue);

            if (thisDateValue > otherDateValue)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }
    }

    public class NumberGreaterThenAttribute : ValidationAttribute
    {
        private readonly string _otherProperty;
        private string _otherPropertyDisplayName;
        private const string _defaultErrorMessage = "{0} must be greater than {1}";

        public NumberGreaterThenAttribute(string otherProperty) : base(_defaultErrorMessage)
        {
            _otherProperty = otherProperty;
        }

        public override string FormatErrorMessage(string name)
        {
            return String.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, _otherPropertyDisplayName ?? _otherProperty);
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

            if (_otherPropertyDisplayName == null)
            {
                _otherPropertyDisplayName = GetDisplayNameForProperty(validationContext.ObjectType, _otherProperty);
            }

            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }

        private static string GetDisplayNameForProperty(Type containerType, string propertyName)
        {
            ICustomTypeDescriptor typeDescriptor = GetTypeDescriptor(containerType);
            PropertyDescriptor property = typeDescriptor.GetProperties().Find(propertyName, true);

            if (property == null)
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture,
                    "Property Not Found", containerType.FullName, propertyName));
            }

            IEnumerable<Attribute> attributes = property.Attributes.Cast<Attribute>();
            DisplayAttribute display = attributes.OfType<DisplayAttribute>().FirstOrDefault();

            if (display != null)
            {
                return display.GetName();
            }

            DisplayNameAttribute displayName = attributes.OfType<DisplayNameAttribute>().FirstOrDefault();
            if (displayName != null)
            {
                return displayName.DisplayName;
            }

            return propertyName;
        }

        private static ICustomTypeDescriptor GetTypeDescriptor(Type type)
        {
            return new AssociatedMetadataTypeTypeDescriptionProvider(type).GetTypeDescriptor(type);
        }
    }

    public class OverlapingRangeAttribute : ValidationAttribute
    {
        private readonly string _key;
        private readonly string _otherProperty;
        private const string _defaultErrorMessage = "Ranges must not overlap with existing ones";

        public OverlapingRangeAttribute(string otherProperty, string key) : base(_defaultErrorMessage)
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

            var db = new ApplicationDbContext();
            var testThisNumber = db.MessageCost
                .Where(mc => 
                    mc.NumberOfMessagesFrom <= rangeValue && mc.NumberOfMessagesTo >= rangeValue && 
                    mc.NumberTypeID == numberTypeValue &&
                    mc.MessageCostID != messageCostId &&
                    mc.EndDate == null)
                .FirstOrDefault();

            if (testThisNumber != null)
            {
                return new ValidationResult(String.Format(CultureInfo.CurrentCulture, Resources.Resources.OverlapingRange));
            }

            return ValidationResult.Success;
        }
    }

    public class CheckPriceAttribute : ValidationAttribute
    {
        private readonly string _price;
        private readonly string _numberOfMessagesTo;
        private readonly string _numberTypeID;

        private const string _defaultErrorMessage = "Price can not be bigger then price with smaller range";

        public CheckPriceAttribute(string numberOfMessagesTo, string numberTypeID) : base(_defaultErrorMessage)
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

            var db = new ApplicationDbContext();
            var testPrice = db.MessageCost
                .Where(mc =>
                    mc.NumberOfMessagesTo < numberOfMessagesTo &&
                    mc.NumberTypeID == numberTypeID &&
                    mc.Price <= price &&
                    mc.EndDate == null)
                .FirstOrDefault();

            if (testPrice != null)
            {
                return new ValidationResult(String.Format(CultureInfo.CurrentCulture, Resources.Resources.PriceError));
            }

            return ValidationResult.Success;
        }
    }
}