using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using BizSMS.Attributes;
using BizSMS.Helpers;
using BizSMS.Models;
using System.ServiceModel.Channels;
using System.Configuration;

namespace BizSMS.Controllers
{
    public class HomeController : BaseController
    {
        [AuthorizeUser]
        public ActionResult Index(ManageMessageId? message)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }
            else
            {
                ViewBag.StatusMessage = message == ManageMessageId.ChangePasswordSuccess ? Resources.Resources.PasswordChangedMessage :
                message == ManageMessageId.SetPasswordSuccess ? Resources.Resources.ResetPasswordSuccess : "";
                return View();
            }

            
        }

        [AuthorizeUser(Roles = "Client,User")]
        public ActionResult SendSMS()
        {
            return View();
        }

        [AuthorizeUser(Roles = "Client,User")]
        public ActionResult SentSmsReport()
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

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
                             //message length
                             //u slucaju da je u tabeli message_numbers messageLengthNT == 0 onda uzmi vrednost messageLength iz message tabele:
                             NumberOfDeliveredMessagesLength = report.Sum(mn => (mn.MessageLengthNT != 0 ? mn.MessageLengthNT : mn.Message.MessageLength) * (mn.Delivered != 1 ? 0 : 1)),
                             Charged = report.Key.Charged
                             //nakon redjanja po godini i mesecu potrebno je poredjati ih i po NumberTypeId desc a onda i po Charged isto desc, jer moze u suprotnom razdvajati jedan mesec placene na vise delova u zavisnosti da li stizu i nenaplacene u sredini
                         }).ToList().OrderByDescending(rpt => rpt.SendYear).ThenByDescending(rpt => rpt.SendMonth).ThenByDescending(rpt => rpt.NumberTypeID).ThenByDescending(rpt => rpt.Charged);

            List<SentMessageReportViewModel> viewModel = new List<SentMessageReportViewModel>();

            if (model == null || model.Count() == 0) 
                return View(viewModel);
            string currentMonthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(model.FirstOrDefault().SendMonth) + " " + model.FirstOrDefault().SendYear + (model.FirstOrDefault().Charged ? "" : " (" + Resources.Resources.FreeOfCharge + ")");

            MessageData clientMessageDetails = new MessageData();

            foreach (var m in model)
            {
                DateTime SendDate = new DateTime(m.SendYear, m.SendMonth, 1);

                string monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.SendMonth) + " " + m.SendYear;
                monthName += m.Charged ? "" : " (" + Resources.Resources.FreeOfCharge + ")";

                var costPrice = db.MessageCost
                        .Where(cost => cost.NumberTypeID == m.NumberTypeID && ((cost.StartDate <= SendDate && cost.EndDate >= SendDate) || (cost.StartDate <= SendDate && cost.EndDate == null))
                        && cost.NumberOfMessagesFrom <= m.NumberOfDeliveredMessagesLength && cost.NumberOfMessagesTo >= m.NumberOfDeliveredMessagesLength)
                        .FirstOrDefault();

                if (costPrice != null)
                {
                    clientMessageDetails.Price = costPrice.Price;
                }
                else
                {
                    clientMessageDetails.Price = 0.00d;
                }

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
                        //NumberTypeName = db.NumberType.Find(m.NumberTypeID).Name,
                        Cost = clientMessageDetails.Sum,
                        Charged = clientMessageDetails.Charged
                    });

                    //price is multiplied with length of messages
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
                            //NumberTypeName = db.NumberType.Find(m.NumberTypeID).Name,
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
                            //NumberTypeName = db.NumberType.Find(m.NumberTypeID).Name,
                            Cost = clientMessageDetails.Sum,
                            Charged = clientMessageDetails.Charged
                        });
                    }
                }
            }
            
            return View(viewModel);
        }

        [AuthorizeUser(Roles = "Client,User")]
        public ActionResult SentSmsDetail(int year, int month, bool charged)
        {

            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
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
                                              //u slucaju da je u tabeli message_numbers messageLengthNT == 0 onda uzmi vrednost messageLength iz message tabele:
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
                double price = 0.00d;
                var costPrice = db.MessageCost
                        .Where(cost => cost.NumberTypeID == m.NumberTypeID && ((cost.StartDate <= SendDate && cost.EndDate >= SendDate) || (cost.StartDate <= SendDate && cost.EndDate == null))
                        && cost.NumberOfMessagesFrom <= m.MessageLengthOfDelivered && cost.NumberOfMessagesTo >= m.MessageLengthOfDelivered)
                        .FirstOrDefault();

                if (costPrice != null)
                {
                    price = costPrice.Price;
                }
                else
                {
                    price = 0.00d;
                }
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
        public ActionResult SentSMSJobReport()
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            
            var messageNumbers = db.Numbers.Where(n => n.ClientID == clientID)
                //.Where(bb => bb.NumberID < 206000)
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
                 SmsSchUser = mesnum.SchSms.User.UserName.ToString(),
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
                var vpnCount = numType.Find(nt => nt.ID == m.Key && (NumberType)nt.NumberTypeID == NumberType.VPN);
                var inMtsCount = numType.Find(nt => nt.ID == m.Key && (NumberType)nt.NumberTypeID == NumberType.U_MTS);
                var outMtsCount = numType.Find(nt => nt.ID == m.Key && (NumberType)nt.NumberTypeID == NumberType.VAN_MTS);

                string status = GetStatus(message.Status);

                var note = "";
                if (status == Resources.Resources.ScheduledSendingCanceled)
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
            switch(status)
            {
                case (int)MessageStatus.Queued:
                    return Resources.Resources.Qued;
                case (int)MessageStatus.Scheduled:
                    return Resources.Resources.Scheduled;
                case (int)MessageStatus.Processing:
                    return Resources.Resources.Processing;
                case (int)MessageStatus.Finished:
                    return Resources.Resources.Finished;
                case (int)MessageStatus.ScheduledSendingCanceled:
                    return Resources.Resources.ScheduledSendingCanceled;
            }

            return Resources.Resources.Error;
        }

        //GET: /Home/SentSMSJobDetailReport/{MessageID}
        [AuthorizeUser(Roles = "Client,User")]
        public ActionResult SentSMSJobDetailReport(int id)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

            var checkUser = db.MessagesNumbers.Where(mn => mn.MessageID == id && mn.Message.User.ClientID == clientID).FirstOrDefault();

            if (checkUser == null)
            {
                throw new HttpException(400, "Bad Request");
            }

            //CheckDeliveryReport(clientID);

            var messageNumbers = db.MessagesNumbers
                .Where(mn => mn.MessageID == id)
                .Select(mn => new
                {
                    Number = mn.NumbersModel.Number,
                    NumberTypeName = mn.NumberType.Name,
                    Name = mn.NumbersModel.Name,
                    MessageText = mn.Message.MessageText,
                    //u slucaju da je u tabeli message_numbers messageLengthNT == 0 onda uzmi vrednost messageLength iz message tabele:
                    MessageLength = mn.MessageLengthNT != 0 ? mn.MessageLengthNT : mn.Message.MessageLength,
                    Status = mn.SendSMSID != "-1" && mn.Sent ? Resources.Resources.Yes : Resources.Resources.No,
                    Delivered = mn.Delivered == 1 ? Resources.Resources.Yes : Resources.Resources.No

                })
                .ToList();
            
            List<SentSMSJobDetailReportViewModel> model = new List<SentSMSJobDetailReportViewModel>();

            var clientIDWithZeroes = clientID.ToString("00000");

            foreach (var mn in messageNumbers)
            {
                var messageTextSuffix = "";

                if (mn.NumberTypeName == "U MTS")
                {
                    messageTextSuffix = Environment.NewLine + ConfigurationManager.AppSettings["unsubscribeTextMTSFirstPart"] + clientIDWithZeroes + ConfigurationManager.AppSettings["unsubscribeTextMTSLastPart"];
                }
                else if (mn.NumberTypeName == "VAN MTS")
                {
                    messageTextSuffix = Environment.NewLine + ConfigurationManager.AppSettings["unsubTextNotInMtsFirstPart"] + clientIDWithZeroes + ConfigurationManager.AppSettings["unsubTextNotInMtsLastPart"];
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
        public FileResult DownloadManual()
        {
            return File("~/UploadedFiles/KorisnickoUputstvoBizSMS_klijent.pdf", "application/pdf");
        }

        public ActionResult SetCulture(string culture)
        {
            // Validate input
            culture = CultureHelper.GetImplementedCulture(culture);
            // Save culture in a cookie
            HttpCookie cookie = Request.Cookies["_culture"];
            if (cookie != null)
                cookie.Value = culture;   // update cookie value
            else
            {
                cookie = new HttpCookie("_culture");
                cookie.Value = culture;
                //cookie.Expires = DateTime.Now.AddYears(1);
            }
            Response.Cookies.Add(cookie);
            return RedirectToAction("Index");
        }

        public class MessageData
        {
            public MessageData()
            {
                Year = 0;
                Price = 0.00d;
                Sum = 0.00d;
                Year = 0;
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
            public void AddToSum(double sum)
            {
                Sum += sum;
            }
            public void AddToNumberOfDeliveredMessages(int number)
            {
                NumberOfDeliveredMessages += number;
            }
            public void AddToNumberOfDeliveredMessagesLength(int number)
            {
                NumberOfDeliveredMessagesLength += number;
            }
            public void AddToNumberOfSentSMSes(int number)
            {
                NumberOfSentSMSes += number;
            }
        }
    }
}