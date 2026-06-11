using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BizSMSReporting
{
    class Program
    {
        private static IConfiguration _configuration;
        static void Main(string[] args)
        {
            GetAppSettingsFile();

            if (DateTime.Today.Day == Convert.ToInt16(_configuration.GetSection("reportSendingDay").Value))
            {
                SendReport();
            }

            CheckNewClients();
        }

        static void GetAppSettingsFile()
        {
            var builder = new ConfigurationBuilder()
                                 .SetBasePath(Directory.GetCurrentDirectory())
                                 .AddJsonFile("appsettings.json", optional: false);
            _configuration = builder.Build();
        }

        private static void CheckNewClients()
        {
            var clients = new Clients(_configuration);
            var sendIds = new SendEmailHelper(_configuration);

            try
            {
                //uzmi nove clientId ako ih ima
                var newClientIds = clients.GetNewClientIds();

                if (newClientIds.Count() != 0)
                {
                    sendIds.SendNewClientIdsMail(newClientIds);
                }
            }
            catch (ApplicationException error)
            {
                sendIds.SendErrorMail(error.Message);
            }
        }

        private static void SendReport()
        {
            int month = (DateTime.Today.Month - 1) == 0 ? 12 : (DateTime.Today.Month - 1);
            int year = (DateTime.Today.Month - 1) == 0 ? (DateTime.Today.Year - 1) : DateTime.Today.Year;
            string[] monthNames = { "Januar", "Februar", "Mart", "April", "Maj", "Jun", "Jul", "Avgust", "Septembar", "Oktobar", "Novembar", "Decembar" };
            string filePath = _configuration.GetSection("filepathToReport").Value + monthNames[month - 1] + year + ".xlsx";

            var report = new Reports(_configuration);
            var sendReport = new SendEmailHelper(_configuration);

            try
            {
                //kreiraj report
                report.CreateMonthlyReport(month, year, filePath);

                //posalji report
                sendReport.SendCreatedReport(month, year, monthNames, filePath);
            }
            catch(ApplicationException aex)
            {
                sendReport.SendErrorMail(aex.Message);
            }
        }        
    }
}
