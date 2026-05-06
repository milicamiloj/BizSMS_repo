using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BizSMS.Models
{
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
        public bool VpnGroupSending { get; set; }
    }

    public class GroupSMSData : SMSData
    {
        public int GroupId { get; set; }
    }

    public class SendingNumbersCheck
    {
        public List<int> NumbersToCheck {get; set;}
        public int? GroupId { get; set; }
    }
}