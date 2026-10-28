#nullable disable
using System.Collections.Generic;

namespace BizSMS.Web.Models
{
    // Zamena za legacy BizSMS.Models (Models/SendSMSModel.cs) — DTO-ovi za slanje SMS-a.
    // Koriste ih read-only i (u Fazi 5) send API endpointi HomeApiController-a.
    public class SendSMSModel
    {
        public string Alphanumeric { get; set; }
        public string Message { get; set; }
        public int MessageLength { get; set; }
    }

    public class TestSMSData : SendSMSModel
    {
        public string PhoneNumber { get; set; }
    }

    public class SMSData : SendSMSModel
    {
        public List<int> PhoneNumbers { get; set; }
        public string ScheduledDateTime { get; set; }
    }

    public class GroupSMSData : SMSData
    {
        public int GroupId { get; set; }
    }

    public class SendingNumbersCheck
    {
        public List<int> NumbersToCheck { get; set; }
        public int? GroupId { get; set; }
    }
}
