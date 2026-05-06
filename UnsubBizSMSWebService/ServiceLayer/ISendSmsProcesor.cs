using UnsubBizSMSWebService.SendSMSReference;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public interface ISendSmsProcesor
    {
        string Username { get; set; }
        string Password { get; set; }
        string[] Msisdn { get; set; }
        string SenderName { get; set; }
        string MsgText { get; set; }
        SimpleReference ReceiptRequest { get; set; }
        ChargingInformation Charging { get; set; }

        string SendSMS();
    }
}
