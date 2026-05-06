using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;
using UnsubBizSMSWebService.SendEmailReference;
//using UnsubBizSMSWebService.SendEmailReference;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public class SendErrorMail
    {
        // slanje mail-ova u slucaju sistemskih gresaka
        public static void SendMailSystem(string info, string description)
        {
            bool rez = false;

            try
            {
                MailService errorMail = new MailService();

                var fromAddress = ConfigurationManager.AppSettings["fromAddress"];
                var fromName = ConfigurationManager.AppSettings["fromName"];
                var toAddress = ConfigurationManager.AppSettings["toAddress"];
                var toName = "Ivan Trickovic";
                var ccAddress = "";
                var ccName = "";
                var subject = info;
                var body = description;
                errorMail.SendEmail(fromAddress, fromName, toAddress, toName, ccAddress, ccName, subject, body);

                rez = true;

                // loguju se detalji o slanju alert mail-a o neuspesnoj odjavi usera InMTS / NotInMTS
                Logger.LogMessage("Sending mail alert about unsuccessfull unsubscription of user InMTS / NotInMTS. Success sending mail alert: " + rez + ", Info: " + info + ", Description: " + description);
            }

            catch(Exception ex)
            {
                // loguju se detalji o slanju alert mail-a o neuspesnoj odjavi usera InMTS / NotInMTS
                Logger.LogMessage("Unsuccessfull sending of mail alert about failed unsubscription of user InMTS / NotInMTS. Info: " + info + ", Error Description: " + ex);
            }

            
        }
    }
}