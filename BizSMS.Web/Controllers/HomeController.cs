#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BizSMS.Web.Data;
using BizSMS.Web.Filters;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BizSMS.Web.Controllers
{
    // Zamena za legacy BizSMS.Controllers.HomeController (MVC deo).
    // Dashboard + izveštaji o poslatim SMS porukama + preuzimanje uputstva.
    // NAPOMENA: slanje SMS-a (SendSMS forma + API/HomeController) zavisi od Hangfire-a i SDP SOAP
    // kampanjskog klijenta -> to se portuje u Fazi 5 (§9.2/§10.1). SendSMS akcija ovde samo renderuje formu.
    public class HomeController : BaseController
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public HomeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
            IConfiguration config, IWebHostEnvironment env)
            : base(db, userManager)
        {
            _config = config;
            _env = env;
        }

        [AuthorizeUser]
        public IActionResult Index(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Lozinka je uspešno promenjena." :
                message == ManageMessageId.SetPasswordSuccess ? "Lozinka je uspešno resetovana" : "";
            return View();
        }

        [AuthorizeUser(Roles = "Client,User")]
        public IActionResult SendSMS()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AuthorizeUser(Roles = "Client,User")]
        public IActionResult SentSmsReport()
        {
            int clientID = CurrentClientId;

            var model = (from numbers in db.Numbers
                         join numbers_messages in db.MessagesNumbers
                         on numbers.NumberID equals numbers_messages.NumberID
                         join messages in db.Message
                         on numbers_messages.MessageID equals messages.MessageID
                         where numbers.ClientID == clientID && numbers_messages.Sent == true
                         group numbers_messages by new { numbers_messages.NumberTypeID, numbers_messages.Charged, numbers_messages.SendDate.Month, numbers_messages.SendDate.Year }
                        into report
                         select new
                         {
                             SendMonth = report.Key.Month == 0 ? 1 : report.Key.Month,
                             SendYear = report.Key.Year,
                             NumberTypeID = report.Key.NumberTypeID,
                             NumberOfDeliveredMessages = report.Sum(nm => nm.Delivered == 1 ? 1 : 0),
                             NumberOfSentSMSes = report.Sum(m => m.Sent ? 1 : 0),
                             // Ako je MessageLengthNT == 0 uzmi MessageLength iz Message tabele:
                             NumberOfDeliveredMessagesLength = report.Sum(mn => (mn.MessageLengthNT != 0 ? mn.MessageLengthNT : mn.Message.MessageLength) * (mn.Delivered != 1 ? 0 : 1)),
                             Charged = report.Key.Charged
                         }).ToList().OrderByDescending(rpt => rpt.SendYear).ThenByDescending(rpt => rpt.SendMonth).ThenByDescending(rpt => rpt.NumberTypeID).ThenByDescending(rpt => rpt.Charged);

            List<SentMessageReportViewModel> viewModel = new List<SentMessageReportViewModel>();

            if (model == null || model.Count() == 0)
                return View(viewModel);

            string currentMonthName = GetSerbianMonthName(model.FirstOrDefault().SendMonth) + " " + model.FirstOrDefault().SendYear + (model.FirstOrDefault().Charged ? "" : " (Besplatne)");

            MessageData clientMessageDetails = new MessageData();

            foreach (var m in model)
            {
                DateTime SendDate = new DateTime(m.SendYear, m.SendMonth, 1);

                string monthName = GetSerbianMonthName(m.SendMonth) + " " + m.SendYear;
                monthName += m.Charged ? "" : " (Besplatne)";

                var costPrice = db.MessageCost
                        .Where(cost => cost.NumberTypeID == m.NumberTypeID && ((cost.StartDate <= SendDate && cost.EndDate >= SendDate) || (cost.StartDate <= SendDate && cost.EndDate == null))
                        && cost.NumberOfMessagesFrom <= m.NumberOfDeliveredMessagesLength && cost.NumberOfMessagesTo >= m.NumberOfDeliveredMessagesLength)
                        .FirstOrDefault();

                clientMessageDetails.Price = costPrice != null ? costPrice.Price : 0.00d;

                if (currentMonthName != monthName)
                {
                    viewModel.Add(new SentMessageReportViewModel()
                    {
                        SendYear = clientMessageDetails.Year,
                        SendMonth = clientMessageDetails.Month,
                        SendMonthName = currentMonthName,
                        NumberOfDeliveredMessages = clientMessageDetails.NumberOfDeliveredMessages,
                        NumberOfDeliveredMessagesLength = clientMessageDetails.NumberOfDeliveredMessagesLength,
                        NumberOfSentSMSes = clientMessageDetails.NumberOfSentSMSes,
                        Cost = clientMessageDetails.Sum,
                        Charged = clientMessageDetails.Charged
                    });

                    clientMessageDetails.Sum = clientMessageDetails.Price * (m.Charged ? m.NumberOfDeliveredMessagesLength : 0);
                    clientMessageDetails.NumberOfDeliveredMessages = m.NumberOfDeliveredMessages;
                    clientMessageDetails.NumberOfDeliveredMessagesLength = m.NumberOfDeliveredMessagesLength;
                    clientMessageDetails.NumberOfSentSMSes = m.NumberOfSentSMSes;
                    clientMessageDetails.Year = m.SendYear;
                    clientMessageDetails.Month = m.SendMonth;
                    currentMonthName = monthName;
                    clientMessageDetails.Charged = m.Charged;

                    if (m.Equals(model.Skip(model.Count() - 1).FirstOrDefault()))
                    {
                        viewModel.Add(new SentMessageReportViewModel()
                        {
                            SendYear = clientMessageDetails.Year,
                            SendMonth = clientMessageDetails.Month,
                            SendMonthName = currentMonthName,
                            NumberOfDeliveredMessages = clientMessageDetails.NumberOfDeliveredMessages,
                            NumberOfDeliveredMessagesLength = clientMessageDetails.NumberOfDeliveredMessagesLength,
                            NumberOfSentSMSes = clientMessageDetails.NumberOfSentSMSes,
                            Cost = clientMessageDetails.Sum,
                            Charged = clientMessageDetails.Charged
                        });
                    }
                }
                else
                {
                    clientMessageDetails.AddToSum(clientMessageDetails.Price * (m.Charged ? m.NumberOfDeliveredMessagesLength : 0));
                    clientMessageDetails.AddToNumberOfDeliveredMessages(m.NumberOfDeliveredMessages);
                    clientMessageDetails.AddToNumberOfDeliveredMessagesLength(m.NumberOfDeliveredMessagesLength);
                    clientMessageDetails.AddToNumberOfSentSMSes(m.NumberOfSentSMSes);
                    clientMessageDetails.Year = m.SendYear;
                    clientMessageDetails.Month = m.SendMonth;
                    clientMessageDetails.Charged = m.Charged;

                    if (m.Equals(model.Skip(model.Count() - 1).FirstOrDefault()))
                    {
                        viewModel.Add(new SentMessageReportViewModel()
                        {
                            SendYear = clientMessageDetails.Year,
                            SendMonth = clientMessageDetails.Month,
                            SendMonthName = currentMonthName,
                            NumberOfDeliveredMessages = clientMessageDetails.NumberOfDeliveredMessages,
                            NumberOfDeliveredMessagesLength = clientMessageDetails.NumberOfDeliveredMessagesLength,
                            NumberOfSentSMSes = clientMessageDetails.NumberOfSentSMSes,
                            Cost = clientMessageDetails.Sum,
                            Charged = clientMessageDetails.Charged
                        });
                    }
                }
            }

            return View(viewModel);
        }

        [AuthorizeUser(Roles = "Client,User")]
        public IActionResult SentSmsDetail(int year, int month, bool charged)
        {
            int clientID = CurrentClientId;
            ViewBag.MonthName = GetSerbianMonthName(month) + " " + year;
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
                             MessageLengthOfDelivered = (from nRpt in report
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
                                             select nRpt.NumberType.NumberTypeID).FirstOrDefault(),
                         }).ToList();

            List<SentMessageDetailReportViewModel> viewModel = new List<SentMessageDetailReportViewModel>();

            foreach (var m in model)
            {
                DateTime SendDate = new DateTime(m.SendYear, m.SendMonth, 1);
                double price;
                var costPrice = db.MessageCost
                        .Where(cost => cost.NumberTypeID == m.NumberTypeID && ((cost.StartDate <= SendDate && cost.EndDate >= SendDate) || (cost.StartDate <= SendDate && cost.EndDate == null))
                        && cost.NumberOfMessagesFrom <= m.MessageLengthOfDelivered && cost.NumberOfMessagesTo >= m.MessageLengthOfDelivered)
                        .FirstOrDefault();

                price = costPrice != null ? costPrice.Price : 0.00d;

                viewModel.Add(new SentMessageDetailReportViewModel()
                {
                    Number = m.Number,
                    NumberOfMessages = m.NumberOfMessages,
                    NumberOfDelivered = m.NumberOfDelivered,
                    NumberTypeName = m.NumberTypeName,
                    NumberOfDeliveredMessagesLength = m.MessageLengthOfDelivered,
                    PriceOfDeliveredMessagesLength = m.MessageLengthOfDelivered * price * (charged == true ? 1 : 0)
                });
            }

            return View(viewModel);
        }

        [AuthorizeUser(Roles = "Client,User")]
        public IActionResult SentSMSJobReport()
        {
            int clientID = CurrentClientId;

            var messageNumbers = db.Numbers.Where(n => n.ClientID == clientID)
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
             }).ToList();

            List<SentSMSJobReportViewModel> model = new List<SentSMSJobReportViewModel>();

            if (messageNumbers == null || messageNumbers.Count == 0)
                return View(model);

            var numType = messageNumbers
                .GroupBy(mn => new { mn.ID, mn.NumberTypeID })
                .DefaultIfEmpty()
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

        //GET: /Home/SentSMSJobDetailReport/{MessageID}
        [AuthorizeUser(Roles = "Client,User")]
        public IActionResult SentSMSJobDetailReport(int id)
        {
            int clientID = CurrentClientId;

            var checkUser = db.MessagesNumbers.Where(mn => mn.MessageID == id && mn.Message.User.ClientID == clientID).FirstOrDefault();

            if (checkUser == null)
            {
                return BadRequest();
            }

            var messageNumbers = db.MessagesNumbers
                .Where(mn => mn.MessageID == id)
                .Select(mn => new
                {
                    Number = mn.NumbersModel.Number,
                    NumberTypeName = mn.NumberType.Name,
                    Name = mn.NumbersModel.Name,
                    MessageText = mn.Message.MessageText,
                    // Ako je MessageLengthNT == 0 uzmi MessageLength iz Message tabele:
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

        [AuthorizeUser(Roles = "Client,User")]
        public IActionResult DownloadManual()
        {
            var path = Path.Combine(_env.ContentRootPath, "UploadedFiles", "KorisnickoUputstvoBizSMS_klijent.pdf");
            return PhysicalFile(path, "application/pdf");
        }

        // Postavlja jezički kolačić. Napomena: efektivno tek kad se poveže UseRequestLocalization (§8.1).
        public IActionResult SetCulture(string culture)
        {
            var implemented = new[] { "sr", "en" };
            if (!implemented.Contains(culture))
                culture = "sr";

            Response.Cookies.Append("_culture", culture);
            return RedirectToAction("Index");
        }

        // Srpski naziv meseca (nezavisno od kulture procesa dok se ne poveže UseRequestLocalization).
        private static string GetSerbianMonthName(int month)
        {
            string[] months = { "januar", "februar", "mart", "april", "maj", "jun",
                                "jul", "avgust", "septembar", "oktobar", "novembar", "decembar" };
            return (month >= 1 && month <= 12) ? months[month - 1] : month.ToString();
        }

        public class MessageData
        {
            public MessageData()
            {
                Year = 0;
                Price = 0.00d;
                Sum = 0.00d;
                Month = 0;
                NumberOfDeliveredMessages = 0;
                NumberOfDeliveredMessagesLength = 0;
                NumberOfSentSMSes = 0;
                Charged = true;
            }
            public int Year { get; set; }
            public int Month { get; set; }
            public int NumberOfDeliveredMessages { get; set; }
            public int NumberOfDeliveredMessagesLength { get; set; }
            public int NumberOfSentSMSes { get; set; }
            public bool Charged { get; set; }
            public double Price { get; set; }
            public double Sum { get; set; }
            public void AddToSum(double sum) => Sum += sum;
            public void AddToNumberOfDeliveredMessages(int number) => NumberOfDeliveredMessages += number;
            public void AddToNumberOfDeliveredMessagesLength(int number) => NumberOfDeliveredMessagesLength += number;
            public void AddToNumberOfSentSMSes(int number) => NumberOfSentSMSes += number;
        }
    }
}
