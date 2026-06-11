using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BizSMS.Models
{
    public class BizSMSViewModels
    {
        public class SentMessageReportViewModel
        {
            public int ClientID { get; set; }
            public int NumberTypeID { get; set; }
            public int NumberOfMessages { get; set; }
            public double Cost { get; set; }
        }

        public class SentSMSReportJobViewModel
        {
            public int ID { get; set; }
            public int NumberTypeID { get; set; }
            public int NumberOfMessages { get; set; }
            public double Cost { get; set; }
        }
    }
    

}