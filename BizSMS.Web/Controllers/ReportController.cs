#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using BizSMS.Web.Data;
using BizSMS.Web.Filters;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BizSMS.Web.Controllers
{
    // Zamena za legacy BizSMS.Controllers.ReportController.
    // Administratorski izveštaji o slanju SMS-a po klijentu (read-only; nema Hangfire/SOAP zavisnosti).
    [AuthorizeUser(Roles = "Administrator")]
    public class ReportController : BaseController
    {
        private readonly IConfiguration _config;

        public ReportController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration config)
            : base(db, userManager)
        {
            _config = config;
        }

        public IActionResult SentSmsClientList()
        {
            logger.SetControllerAction("ReportController", "SentSmsClientList");
            var clients = db.Client.Where(c => c.Name != "Telekom");
            List<AdminManageClientsViewModel> model = new List<AdminManageClientsViewModel>();

            foreach (var client in clients)
            {
                model.Add(new AdminManageClientsViewModel
                {
                    ClientID = client.ClientID,
                    Name = client.Name,
                    MtsID = client.MtsID,
                    PhoneNumber = client.PhoneNumber,
                    IsCanceled = client.IsCanceled
                });
            }
            logger.Info("Prikaz liste klijenata");
            return View(model);
        }

        public IActionResult SentSmsReport(int id)
        {
            logger.SetControllerAction("ReportController", "SentSmsReport");
            var client = db.Client.Find(id);

            if (client == null)
            {
                logger.Warn("Klijent nije pronađen");
                return NotFound();
            }

            ViewBag.ClientName = client.Name;
            int clientID = id;
            ViewBag.ClientId = clientID;

            var model = (from numbers in db.Numbers
                         join numbers_messages in db.MessagesNumbers
                         on numbers.NumberID equals numbers_messages.NumberID
                         where numbers.ClientID == client.ClientID && numbers_messages.Sent == true
                         group numbers_messages by new { numbers_messages.NumberTypeID, numbers_messages.Charged, numbers_messages.SendDate.Month, numbers_messages.SendDate.Year }
                        into report
                         select new
                         {
                             SendMonth = report.Key.Month,
                             SendYear = report.Key.Year,
                             NumberTypeID = report.Key.NumberTypeID,
                             NumberOfDeliveredMessages = report.Sum(nm => nm.Delivered == 1 ? 1 : 0),
                             NumberOfSentSMSes = report.Sum(m => m.Sent ? 1 : 0),
                             NumberOfDeliveredMessagesLength = report.Sum(mn => (mn.MessageLengthNT != 0 ? mn.MessageLengthNT : mn.Message.MessageLength) * (mn.Delivered != 1 ? 0 : 1)),
                             Charged = report.Key.Charged
                         }).ToList().OrderByDescending(rpt => rpt.SendYear).ThenByDescending(rpt => rpt.SendMonth).ThenByDescending(rpt => rpt.NumberTypeID).ThenByDescending(rpt => rpt.Charged);

            List<SentMessageReportViewModel> viewModel = new List<SentMessageReportViewModel>();

            if (model == null || model.Count() == 0)
                return View(viewModel);

            string currentMonthName = GetSerbianMonthName(model.FirstOrDefault().SendMonth) + " " + model.FirstOrDefault().SendYear + (model.FirstOrDefault().Charged ? "" : " (Besplatne)");
            double price = 0.00d;
            double sum = 0.00d;
            var year = 0;
            var month = 0;
            var numberOfDeliveredMessages = 0;
            var numberOfDeliveredMessagesLength = 0;
            var numberOfSentSMSes = 0;
            var charged = true;

            foreach (var m in model)
            {
                DateTime SendDate = new DateTime(m.SendYear, m.SendMonth, 1);

                string monthName = GetSerbianMonthName(m.SendMonth) + " " + m.SendYear;
                monthName += m.Charged ? "" : " (Besplatne)";

                var costPrice = db.MessageCost
                        .Where(cost => cost.NumberTypeID == m.NumberTypeID && ((cost.StartDate <= SendDate && cost.EndDate >= SendDate) || (cost.StartDate <= SendDate && cost.EndDate == null))
                        && cost.NumberOfMessagesFrom <= m.NumberOfDeliveredMessagesLength && cost.NumberOfMessagesTo >= m.NumberOfDeliveredMessagesLength)
                        .FirstOrDefault();

                price = costPrice != null ? costPrice.Price : 0.00d;

                if (currentMonthName != monthName)
                {
                    viewModel.Add(new SentMessageReportViewModel()
                    {
                        SendYear = year,
                        SendMonth = month,
                        SendMonthName = currentMonthName,
                        NumberOfDeliveredMessages = numberOfDeliveredMessages,
                        NumberOfDeliveredMessagesLength = numberOfDeliveredMessagesLength,
                        NumberOfSentSMSes = numberOfSentSMSes,
                        Cost = sum,
                        Charged = charged
                    });

                    sum = price * (m.Charged ? m.NumberOfDeliveredMessagesLength : 0);
                    numberOfDeliveredMessages = m.NumberOfDeliveredMessages;
                    numberOfDeliveredMessagesLength = m.NumberOfDeliveredMessagesLength;
                    numberOfSentSMSes = m.NumberOfSentSMSes;
                    year = m.SendYear;
                    month = m.SendMonth;
                    currentMonthName = monthName;
                    charged = m.Charged;

                    if (m.Equals(model.Skip(model.Count() - 1).FirstOrDefault()))
                    {
                        viewModel.Add(new SentMessageReportViewModel()
                        {
                            SendYear = year,
                            SendMonth = month,
                            SendMonthName = currentMonthName,
                            NumberOfDeliveredMessages = numberOfDeliveredMessages,
                            NumberOfDeliveredMessagesLength = numberOfDeliveredMessagesLength,
                            NumberOfSentSMSes = numberOfSentSMSes,
                            Cost = sum,
                            Charged = charged
                        });
                    }
                }
                else
                {
                    sum += price * (m.Charged ? m.NumberOfDeliveredMessagesLength : 0);
                    numberOfDeliveredMessages += m.NumberOfDeliveredMessages;
                    numberOfDeliveredMessagesLength += m.NumberOfDeliveredMessagesLength;
                    numberOfSentSMSes += m.NumberOfSentSMSes;
                    year = m.SendYear;
                    month = m.SendMonth;
                    charged = m.Charged;

                    if (m.Equals(model.Skip(model.Count() - 1).FirstOrDefault()))
                    {
                        viewModel.Add(new SentMessageReportViewModel()
                        {
                            SendYear = year,
                            SendMonth = month,
                            SendMonthName = currentMonthName,
                            NumberOfDeliveredMessages = numberOfDeliveredMessages,
                            NumberOfDeliveredMessagesLength = numberOfDeliveredMessagesLength,
                            NumberOfSentSMSes = numberOfSentSMSes,
                            Cost = sum,
                            Charged = charged
                        });
                    }
                }
            }
            return View(viewModel);
        }

        public IActionResult SentSmsDetail(int clientId, int year, int month, bool charged)
        {
            logger.SetControllerAction("ReportController", "SentSmsDetail");
            var client = db.Client.Find(clientId);

            if (client == null)
            {
                logger.Info("Klijent nije pronađen");
                return NotFound();
            }

            ViewBag.ClientName = client.Name;
            ViewBag.ClientId = clientId;
            ViewBag.MonthName = GetSerbianMonthName(month) + " " + year;
            int clientID = clientId;

            var model = (from numbers in db.Numbers
                         join numbers_messages in db.MessagesNumbers
                         on numbers.NumberID equals numbers_messages.NumberID
                         join messages in db.Message
                         on numbers_messages.MessageID equals messages.MessageID
                         where numbers.ClientID == clientID &&
                               numbers_messages.SendDate.Month == month && numbers_messages.SendDate.Year == year &&
                               numbers_messages.Sent == true
                               && numbers_messages.Charged == charged
                         group numbers by numbers.Number
                         into report
                         select new
                         {
                             SendMonth = month,
                             SendYear = year,
                             Number = report.Key,
                             NumberOfMessages = report.Count(),
                             MessageLengthOfDeliveredMessages = (from nRpt in report
                                            select nRpt.MessagesNumbers
                                            .Where(mn =>
                                            mn.Delivered == 1 &&
                                            mn.Charged == charged &&
                                            mn.SendDate.Month == month && mn.SendDate.Year == year)
                                            .Sum(mn => mn.MessageLengthNT != 0 ? mn.MessageLengthNT : mn.Message.MessageLength)).FirstOrDefault(),
                             NumberOfDelivered = (from nRpt in report
                                                  select nRpt.MessagesNumbers
                                                  .Where(mn =>
                                                  mn.Delivered == 1 &&
                                                  mn.NumbersModel.ClientID == clientID &&
                                                  mn.Charged == charged &&
                                                  mn.SendDate.Month == month && mn.SendDate.Year == year)
                                                  .Count()).FirstOrDefault(),
                             NumberTypeName = (from nRpt in report
                                           select nRpt.NumberType.Name).FirstOrDefault(),
                             NumberTypeID = (from nRpt in report
                                               select nRpt.NumberType.NumberTypeID).FirstOrDefault()
                         }).ToList();

            List<SentMessageDetailReportViewModel> viewModel = new List<SentMessageDetailReportViewModel>();

            foreach (var m in model)
            {
                DateTime SendDate = new DateTime(m.SendYear, m.SendMonth, 1);
                double price;
                var costPrice = db.MessageCost
                        .Where(cost => cost.NumberTypeID == m.NumberTypeID && ((cost.StartDate <= SendDate && cost.EndDate >= SendDate) || (cost.StartDate <= SendDate && cost.EndDate == null))
                        && cost.NumberOfMessagesFrom <= m.MessageLengthOfDeliveredMessages && cost.NumberOfMessagesTo >= m.MessageLengthOfDeliveredMessages)
                        .FirstOrDefault();

                price = costPrice != null ? costPrice.Price : 0.00d;

                viewModel.Add(new SentMessageDetailReportViewModel()
                {
                    Number = m.Number,
                    NumberOfMessages = m.NumberOfMessages,
                    NumberOfDelivered = m.NumberOfDelivered,
                    NumberTypeName = m.NumberTypeName,
                    NumberOfDeliveredMessagesLength = m.MessageLengthOfDeliveredMessages,
                    PriceOfDeliveredMessagesLength = m.MessageLengthOfDeliveredMessages * price * (charged == true ? 1 : 0)
                });
            }

            return View(viewModel);
        }

        public IActionResult SentSMSJobReport(int id)
        {
            var client = db.Client.Find(id);

            if (client == null)
            {
                return NotFound();
            }

            ViewBag.ClientName = client.Name;
            int clientID = id;
            ViewBag.ClientId = clientID;

            // Poslednjih šest meseci
            DateTime sixMonthsAgo = DateTime.Now.AddMonths(-6);

            var messageNumbers = db.Numbers
                .Where(n => n.ClientID == clientID)
                .Join(db.MessagesNumbers,
                    nums => nums.NumberID,
                    mesnum => mesnum.NumberID,
                    (nums, mesnum) => new { Nums = nums, Mesnums = mesnum })
                .Join(db.Message,
                    mesnum => mesnum.Mesnums.MessageID,
                    mess => mess.MessageID,
                    (mesnum, mess) => new { mesnum.Nums, mesnum.Mesnums, Mess = mess })
                .GroupJoin(db.ScheduledSms,
                    mess => mess.Mess.MessageID,
                    sch => sch.MessageID,
                    (mess, sch) => new { mess.Mess, mess.Mesnums, SchSms = sch.FirstOrDefault() })
                .Where(mesnum => mesnum.Mess.SendDate >= sixMonthsAgo)
                .Select(mesnum => new
                {
                    ID = mesnum.Mesnums.MessageID,
                    NumberID = mesnum.Mesnums.NumberID,
                    NumberTypeID = mesnum.Mesnums.NumberTypeID,
                    Alphanumeric = mesnum.Mess.Sender,
                    Message = mesnum.Mess.MessageText,
                    SendDate = mesnum.Mess.SendDate,
                    User = mesnum.Mess.User.UserName,
                    Status = mesnum.Mess.Status,
                    SmsSchUser = mesnum.SchSms.User.UserName,
                    SmsSchDate = mesnum.SchSms.CancelDate
                })
                .ToList();

            List<SentSMSJobReportViewModel> model = new List<SentSMSJobReportViewModel>();

            if (messageNumbers == null || messageNumbers.Count == 0)
                return View(model);

            var numType = messageNumbers
                .GroupBy(mn => new { mn.ID, mn.NumberTypeID })
                .Select(mn => new { ID = mn.Key.ID, NumberTypeID = mn.Key.NumberTypeID, Count = mn.Count() })
                .ToList();

            foreach (var m in messageNumbers.GroupBy(mn => mn.ID).ToList())
            {
                var message = messageNumbers.Where(mn => mn.ID == m.Key).FirstOrDefault();
                var vpnCount = numType.Find(nt => nt.ID == m.Key && (NumberType?)nt.NumberTypeID == NumberType.VPN);
                var inMtsCount = numType.Find(nt => nt.ID == m.Key && (NumberType?)nt.NumberTypeID == NumberType.U_MTS);
                var outMtsCount = numType.Find(nt => nt.ID == m.Key && (NumberType?)nt.NumberTypeID == NumberType.VAN_MTS);

                string status = GetStatus(message.Status);

                var note = "";
                if (status == "Otkazan")
                    note = m.Select(sch => sch.SmsSchUser).FirstOrDefault() + " | " + m.Select(sch => sch.SmsSchDate).FirstOrDefault();

                model.Add(new SentSMSJobReportViewModel()
                {
                    ID = m.Key,
                    Alphanumeric = message.Alphanumeric,
                    Message = message.Message,
                    VPN = vpnCount == null ? 0 : vpnCount.Count,
                    InMTS = inMtsCount == null ? 0 : inMtsCount.Count,
                    OutMTS = outMtsCount == null ? 0 : outMtsCount.Count,
                    SendDate = message.SendDate,
                    User = message.User,
                    Status = status,
                    CanceledBy = note
                });
            }

            return View(model.OrderByDescending(m => m.ID));
        }

        public IActionResult SentSMSJobDetailReport(int clientId, int messageId)
        {
            var client = db.Client.Find(clientId);

            if (client == null)
            {
                return NotFound();
            }

            ViewBag.ClientName = client.Name;
            int clientID = clientId;

            var checkUser = db.MessagesNumbers.Where(mn => mn.MessageID == messageId && mn.Message.User.ClientID == clientID).FirstOrDefault();

            if (checkUser == null)
            {
                return BadRequest();
            }

            var messageNumbers = db.MessagesNumbers
                .Where(mn => mn.MessageID == messageId)
                .Select(mn => new
                {
                    Number = mn.NumbersModel.Number,
                    NumberTypeName = mn.NumberType.Name,
                    Name = mn.NumbersModel.Name,
                    MessageText = mn.Message.MessageText,
                    MessageLength = mn.MessageLengthNT != 0 ? mn.MessageLengthNT : mn.Message.MessageLength,
                    Status = mn.SendSMSID != "-1" && mn.Sent ? "Da" : "Ne",
                    Delivered = mn.Delivered == 1 ? "Da" : "Ne"
                })
                .ToList();

            List<SentSMSJobDetailReportViewModel> model = new List<SentSMSJobDetailReportViewModel>();

            var clientIDWithZeroes = clientID.ToString("00000");

            foreach (var mn in messageNumbers)
            {
                var messageTextSuffix = "";

                if (mn.NumberTypeName == "U MTS")
                {
                    messageTextSuffix = Environment.NewLine + _config["Unsubscribe:TextMtsFirstPart"] + clientIDWithZeroes + _config["Unsubscribe:TextMtsLastPart"];
                }
                else if (mn.NumberTypeName == "VAN MTS")
                {
                    messageTextSuffix = Environment.NewLine + _config["Unsubscribe:TextNotInMtsFirstPart"] + clientIDWithZeroes + _config["Unsubscribe:TextNotInMtsLastPart"];
                }

                model.Add(new SentSMSJobDetailReportViewModel()
                {
                    Number = mn.Number,
                    NumberTypeName = mn.NumberTypeName,
                    Name = mn.Name,
                    MessageText = mn.MessageText + messageTextSuffix,
                    MessageLength = mn.MessageLength,
                    Status = mn.Status,
                    Delivered = mn.Delivered
                });
            }

            return View(model);
        }

        private string GetStatus(int status)
        {
            switch (status)
            {
                case (int)MessageStatus.Queued:
                    return "U pripremi";
                case (int)MessageStatus.Scheduled:
                    return "Zakazan";
                case (int)MessageStatus.Processing:
                    return "U obradi";
                case (int)MessageStatus.Finished:
                    return "Završeno";
                case (int)MessageStatus.ScheduledSendingCanceled:
                    return "Otkazan";
            }

            return "Greška";
        }

        private static string GetSerbianMonthName(int month)
        {
            string[] months = { "januar", "februar", "mart", "april", "maj", "jun",
                                "jul", "avgust", "septembar", "oktobar", "novembar", "decembar" };
            return (month >= 1 && month <= 12) ? months[month - 1] : month.ToString();
        }
    }
}
