using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace BizSMSReporting
{
    class SendEmailHelper
    {
        private readonly IConfiguration _configuration;

        public SendEmailHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        readonly MailMessage mailMessage = new MailMessage();
        readonly SmtpClient smtpClient = new SmtpClient();
        public void SendNewClientIdsMail(List<string> newClientIds)
        {
            try
            {
                foreach (string recipient in _configuration.GetSection("listOfRecipientsNewClients").Value.Split(','))
                    mailMessage.To.Add(recipient);
                foreach (string cc in _configuration.GetSection("listOfCCNewClients").Value.Split(','))
                    mailMessage.CC.Add(cc);
                mailMessage.Subject = _configuration.GetSection("messageSubjectNewClients").Value;
                mailMessage.From = new MailAddress(_configuration.GetSection("sender").Value, _configuration.GetSection("projectNameNewClients").Value);

                mailMessage.Body = _configuration.GetSection("messageBody1NewClients").Value + string.Join("<br/>", newClientIds) + _configuration.GetSection("messageBody2NewClients").Value;
                mailMessage.IsBodyHtml = true;
                mailMessage.SubjectEncoding = Encoding.UTF8;
                mailMessage.BodyEncoding = Encoding.UTF8;
                mailMessage.Priority = MailPriority.Normal;

                smtpClient.Host = _configuration.GetSection("host").Value;
                smtpClient.Port = Convert.ToInt16(_configuration.GetSection("port").Value);
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential(_configuration.GetSection("username").Value, _configuration.GetSection("password").Value);
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;

                smtpClient.Send(mailMessage);

                mailMessage.Dispose();
            }
            catch (Exception error)
            {
                throw new ApplicationException("Greska prilikom slanja kodova novih klijenata BizSMS: " + error);
            }


            //ukoliko se slanje vrsi preko web servisa:
            //var sendEmailService = new SendEmailService.ServiceSoapClient(SendEmailService.ServiceSoapClient.EndpointConfiguration.ServiceSoap);

            //SendEmailService.ArrayOfString recipients = new SendEmailService.ArrayOfString();
            //recipients.AddRange(_configuration.GetSection("listOfRecipientsNewClients").Value.Split(','));

            //SendEmailService.ArrayOfString listOfCC = new SendEmailService.ArrayOfString();
            //listOfCC.AddRange(_configuration.GetSection("listOfCCNewClients").Value.Split(','));

            //var emailSent = sendEmailService.SendNotification(
            //    _configuration.GetSection("projectNameNewClients").Value,
            //    _configuration.GetSection("serviceCode").Value,
            //    _configuration.GetSection("messageSubjectNewClients").Value,
            //    _configuration.GetSection("messageBody1NewClients").Value + string.Join("<br/>", newClientIds) + _configuration.GetSection("messageBody2NewClients").Value,
            //    recipients,
            //    listOfCC);

             //if(!emailSent)
             //   throw new ApplicationException("Greska prilikom slanja kodova novih klijenata BizSMS");
        }

        public void SendErrorMail(string error)
        {
            foreach (string recipient in _configuration.GetSection("errorListOfRecipients").Value.Split(','))
                mailMessage.To.Add(recipient);
            foreach (string cc in _configuration.GetSection("errorListOfCC").Value.Split(','))
                mailMessage.CC.Add(cc);
            mailMessage.Subject = _configuration.GetSection("errorMessageSubject").Value;
            mailMessage.From = new MailAddress(_configuration.GetSection("sender").Value, _configuration.GetSection("errorProjectName").Value);

            mailMessage.Body = _configuration.GetSection("errorMessageBody").Value + error;
            mailMessage.IsBodyHtml = true;
            mailMessage.SubjectEncoding = Encoding.UTF8;
            mailMessage.BodyEncoding = Encoding.UTF8;
            mailMessage.Priority = Convert.ToInt16(_configuration.GetSection("errorMessagePriority").Value) == 1 ? MailPriority.High : MailPriority.Normal;

            smtpClient.Host = _configuration.GetSection("host").Value;
            smtpClient.Port = Convert.ToInt16(_configuration.GetSection("port").Value);
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new NetworkCredential(_configuration.GetSection("username").Value, _configuration.GetSection("password").Value);
            smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtpClient.Send(mailMessage);

            mailMessage.Dispose();



            //ukoliko se slanje vrsi preko web servisa:

            //var sendEmailService = new SendEmailService.ServiceSoapClient(SendEmailService.ServiceSoapClient.EndpointConfiguration.ServiceSoap);

            ////formiranje parametara i slanje mejla o gresci
            //SendEmailService.ArrayOfString errorMonthlyReportRecipients = new SendEmailService.ArrayOfString();
            //errorMonthlyReportRecipients.AddRange(_configuration.GetSection("errorListOfRecipients").Value.Split(','));

            //SendEmailService.ArrayOfString errorListOfCCReport = new SendEmailService.ArrayOfString();
            //errorListOfCCReport.AddRange(_configuration.GetSection("errorListOfCC").Value.Split(','));

            //sendEmailService.SendEmailWithPriority(
            //    _configuration.GetSection("errorProjectName").Value,
            //    _configuration.GetSection("serviceCode").Value,
            //    _configuration.GetSection("errorMessageSubject").Value,
            //    _configuration.GetSection("errorMessageBody").Value + error,
            //    errorMonthlyReportRecipients,
            //    errorListOfCCReport,
            //    Convert.ToInt16(_configuration.GetSection("errorMessagePriority").Value)
            //    );
        }

        public void SendCreatedReport(int month, int year, string[] monthNames, string filePath)
        {
            try
            {
                foreach (string recipient in _configuration.GetSection("listOfRecipientsReport").Value.Split(','))
                    mailMessage.To.Add(recipient);
                foreach (string cc in _configuration.GetSection("listOfCCReport").Value.Split(','))
                    mailMessage.CC.Add(cc);
                mailMessage.Subject = _configuration.GetSection("messageSubjectReport").Value;
                mailMessage.From = new MailAddress(_configuration.GetSection("sender").Value, _configuration.GetSection("projectNameReport").Value);
                    
                mailMessage.Body = _configuration.GetSection("messageBody1Report").Value + monthNames[month - 1] + " " + year + _configuration.GetSection("messageBody2Report").Value;
                mailMessage.IsBodyHtml = true;
                mailMessage.SubjectEncoding = Encoding.UTF8;
                mailMessage.BodyEncoding = Encoding.UTF8;
                mailMessage.Priority = MailPriority.Normal;

                Attachment attachment = new Attachment(filePath);
                mailMessage.Attachments.Add(attachment);

                smtpClient.Host = _configuration.GetSection("host").Value;
                smtpClient.Port = int.Parse(_configuration.GetSection("port").Value);
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential(_configuration.GetSection("username").Value, _configuration.GetSection("password").Value);
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;

                smtpClient.Send(mailMessage);

                mailMessage.Dispose();                
            }

            catch (Exception error)
            {
                throw new ApplicationException("Greska prilikom slanja izvestaja za BizSMS, izvestaj nije poslat. Greska: " + error);
            }

            File.Delete(filePath);

            //ukoliko se slanje vrsi preko web servisa:

            //var sendEmailService = new SendEmailService.ServiceSoapClient(SendEmailService.ServiceSoapClient.EndpointConfiguration.ServiceSoap);

            //formiranje potrebnih parametara i slanje izvestaja
            //SendEmailService.ArrayOfString monthlyReportRecipients = new SendEmailService.ArrayOfString();
            //monthlyReportRecipients.AddRange(_configuration.GetSection("listOfRecipientsReport").Value.Split(','));

            //SendEmailService.ArrayOfString listOfCCReport = new SendEmailService.ArrayOfString();
            //listOfCCReport.AddRange(_configuration.GetSection("listOfCCReport").Value.Split(','));

            //var messageBody = _configuration.GetSection("messageBody1Report").Value + monthNames[month - 1] + " " + year + _configuration.GetSection("messageBody2Report").Value;

            //var emailSent = sendEmailService.SendEmailWithAttach(
            //    _configuration.GetSection("projectNameReport").Value,
            //    _configuration.GetSection("serviceCode").Value,
            //    _configuration.GetSection("messageSubjectReport").Value,
            //    messageBody,
            //    monthlyReportRecipients,
            //    listOfCCReport,
            //    filePath);

            //brisanje excel fajla nakon slanja
            //File.Delete(filePath);

            //if (!emailSent)
            //    throw new ApplicationException("Greska prilikom slanja izvestaja za BizSMS, izvestaj nije poslat");
        }
    }
}
