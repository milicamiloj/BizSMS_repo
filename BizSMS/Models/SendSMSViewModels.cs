using System;
using System.ComponentModel.DataAnnotations;

namespace BizSMS.Models
{
    public class SentMessageReportViewModel
    {
        public int SendYear { get; set; }
        public int SendMonth { get; set; }

        [Display(Name = "SendMonth", ResourceType = typeof(Resources.Resources))]
        public string SendMonthName { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberTypeName { get; set; }

        [Display(Name = "NumberOfDeliveredMessages", ResourceType = typeof(Resources.Resources))]
        public int NumberOfDeliveredMessages { get; set; }

        [Display(Name = "NumberOfDeliveredMessagesLength", ResourceType = typeof(Resources.Resources))]
        public int NumberOfDeliveredMessagesLength { get; set; }
        

        [Display(Name = "NumberOfSentSMSes", ResourceType = typeof(Resources.Resources))]
        public int NumberOfSentSMSes { get; set; }
        
        [DisplayFormat(DataFormatString = "{0:#,##0.00#}")]
        [Display(Name = "Cost", ResourceType = typeof(Resources.Resources))]
        public double Cost { get; set; }

        public bool Charged { get; set; }
    }

    public class SentMessageDetailReportViewModel
    {
        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        public string Number { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberTypeName { get; set; }

        [Display(Name = "NumberOfSentSMSes", ResourceType = typeof(Resources.Resources))]
        public int NumberOfMessages { get; set; }

        [Display(Name = "NumberOfDeliveredMessages", ResourceType = typeof(Resources.Resources))]
        public int NumberOfDelivered { get; set; }

        [Display(Name = "NumberOfDeliveredMessagesLength", ResourceType = typeof(Resources.Resources))]
        public int NumberOfDeliveredMessagesLength { get; set; }

        [Display(Name = "PriceOfDeliveredMessagesLength", ResourceType = typeof(Resources.Resources))]
        public double PriceOfDeliveredMessagesLength { get; set; }
    }

    public class SentSMSJobReportViewModel
    {
        public int ID { get; set; }

        [Display(Name = "User", ResourceType = typeof(Resources.Resources))]
        public string User { get; set; }

        [Display(Name = "Alphanumeric", ResourceType = typeof(Resources.Resources))]
        public string Alphanumeric { get; set; }

        [Display(Name = "SendDate", ResourceType = typeof(Resources.Resources))]
        public DateTime SendDate { get; set; }

        [Display(Name = "VPN", ResourceType = typeof(Resources.Resources))]
        public int VPN { get; set; }

        [Display(Name = "InMTS", ResourceType = typeof(Resources.Resources))]
        public int InMTS { get; set; }

        [Display(Name = "OutMTS", ResourceType = typeof(Resources.Resources))]
        public int OutMTS { get; set; }
        
        [Display(Name = "MessageText", ResourceType = typeof(Resources.Resources))]
        public string Message { get; set; }

        public string Status { get; set; }

        [Display(Name = "CanceledBy", ResourceType = typeof(Resources.Resources))]
        public string CanceledBy { get; set; }
    }

    public class SentSMSJobDetailReportViewModel
    {
        [Display(Name = "Number", ResourceType = typeof(Resources.Resources))]
        public string Number { get; set; }

        [Display(Name = "NumberType", ResourceType = typeof(Resources.Resources))]
        public string NumberTypeName { get; set; }

        [Display(Name = "Name", ResourceType = typeof(Resources.Resources))]
        public string Name { get; set; }

        [Display(Name = "MessageText", ResourceType = typeof(Resources.Resources))]
        public string MessageText { get; set; }

        [Display(Name = "MessageLength", ResourceType = typeof(Resources.Resources))]
        public int MessageLength { get; set; }

        [Display(Name = "Sent", ResourceType = typeof(Resources.Resources))]
        public string Status { get; set; }

        [Display(Name = "Delivered", ResourceType = typeof(Resources.Resources))]
        public string Delivered { get; set; }
    }
}