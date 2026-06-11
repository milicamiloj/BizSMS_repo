using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnsubBizSMSWebService.SendSMSReference;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public interface ISendSMSService
    {
        string Username { get; set; }
        string Password { get; set; }
        string[] Msisdn { get; set; }
        string SenderName { get; set; }
        string MsgText { get; set; }
        SimpleReference ReceiptRequest { get; set; }
        SendSMSReference.ChargingInformation Charging { get; set; }
        string SendSMS();
    }
}
