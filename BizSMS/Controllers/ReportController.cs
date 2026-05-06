using BizSMS.Attributes;
using BizSMS.Helpers;
using BizSMS.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace BizSMS.Controllers
{
    [AuthorizeUser(Roles = "Administrator")]
    public class ReportController : BaseController
    {
        readonly Logger logger = new Logger();

        public ActionResult SentSmsClientList()
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
            logger.Info("Show client list");
            return View(model);
        }

        
        public ActionResult SentSmsReport(int id)
        {
            logger.SetControllerAction("ReportController", "SentSmsReport");
            logger.Info("Get client with id: " + id.ToString());
            var client = db.Client.Find(id);
            //int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

            if (client == null)
            {
                logger.Warn("Client not found");
                throw new HttpException(404, "Not found");
            }

            ViewBag.ClientName = client.Name;

            int clientID = id;

            ViewBag.ClientId = clientID;
            logger.Info("Generate report");
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

                string monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.SendMonth) + " " + m.SendYear;
                monthName += m.Charged ? "" : " (" + Resources.Resources.FreeOfCharge + ")";

                var costPrice = db.MessageCost
                        .Where(cost => cost.NumberTypeID == m.NumberTypeID && ((cost.StartDate <= SendDate && cost.EndDate >= SendDate) || (cost.StartDate <= SendDate && cost.EndDate == null))
                        && cost.NumberOfMessagesFrom <= m.NumberOfDeliveredMessagesLength && cost.NumberOfMessagesTo >= m.NumberOfDeliveredMessagesLength)
                        .FirstOrDefault();

                if (costPrice != null)
                {
                    price = costPrice.Price;
                }
                else
                {
                    price = 0.00d;
                }

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
                        //NumberTypeName = db.NumberType.Find(m.NumberTypeID).Name,
                        Cost = sum,
                        Charged = charged
                    });

                    //price is multiplied with length of messages
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
                            //NumberTypeName = db.NumberType.Find(m.NumberTypeID).Name,
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
                            //NumberTypeName = db.NumberType.Find(m.NumberTypeID).Name,
                            Cost = sum,
                            Charged = charged
                        });
                    }
                }
            }
            logger.Info("Report generated");
            return View(viewModel);
        }

        //// GET: Report/SentSmsReport/{ClientId}
        //public ActionResult SentSmsReport(int id)
        //{
        //    var client = db.Client.Find(id);

        //    if(client == null)
        //    {
        //        throw new HttpException(404, "Not found");
        //    }

        //    ViewBag.ClientName = client.Name;

        //    int clientID = id;

        //    ViewBag.ClientId = clientID;



        //    //var firstFiveMessages = db.Message
        //    //    .GroupBy(m => new { m.User.ClientID, m.SendDate.Year, m.SendDate.Month })
        //    //    .OrderBy(g => new { g.Key.ClientID, g.Key.Year, g.Key.Month })
        //    //    .SelectMany(g => g.Select(m => new { ClientId = m.User.ClientID, MessageId = m.MessageID }).Take(5))
        //    //    .ToList();
        //    ////.ToDictionary(c => c.ClientId, c => c.MessageId);

        //    //var test = firstFiveMessages
        //    //             .Where(c => c.ClientId == 12)
        //    //             .Select(m => m.MessageId)
        //    //             .Contains(2);

        //    var model = (from numbers in db.Numbers
        //                 join numbers_messages in db.MessagesNumbers
        //                 on numbers.NumberID equals numbers_messages.NumberID
        //                 where numbers.ClientID == clientID 
        //                 && numbers_messages.Sent == true 
        //                 //&& numbers_messages.Charged == true
        //                 //&& 
        //                 //firstFiveMessages
        //                 //.Where(c => c.ClientId == numbers.ClientID)
        //                 //.Select(m => m.MessageId)
        //                 //.Contains(numbers_messages.MessageID)
        //                 group numbers_messages by new { numbers_messages.NumberTypeID, numbers_messages.Charged, numbers_messages.SendDate.Month, numbers_messages.SendDate.Year }
        //                 into report
        //                 select new
        //                 {
        //                     SendMonth = report.Key.Month,
        //                     SendYear = report.Key.Year,
        //                     NumberTypeID = report.Key.NumberTypeID,
        //                     NumberOfDeliveredMessages = report.Sum(nm => nm.Delivered == 1 ? 1 : 0),
        //                     NumberOfSentSMSes = report.Sum(m => m.Sent ? 1 : 0),
        //                     NumberOfDeliveredMessagesLength = report.Sum(m => m.Message.MessageLength * m.Delivered),
        //                     Charged = report.Key.Charged
        //                 }).ToList().OrderByDescending(rpt => rpt.SendYear).ThenByDescending(rpt => rpt.SendMonth);

        //    List<SentMessageReportViewModel> viewModel = new List<SentMessageReportViewModel>();

        //    double price = 0.00d;

        //    foreach (var m in model)
        //    {
        //        DateTime SendDate = new DateTime(m.SendYear, m.SendMonth, 1);

        //        var costPrice = db.MessageCost
        //            .Where(cost => cost.NumberTypeID == m.NumberTypeID &&
        //            ((cost.StartDate <= SendDate && cost.EndDate >= SendDate)
        //            || (cost.StartDate <= SendDate && cost.EndDate == null))
        //            && cost.NumberOfMessagesFrom <= m.NumberOfDeliveredMessagesLength && cost.NumberOfMessagesTo >= m.NumberOfDeliveredMessagesLength)
        //            .FirstOrDefault();

        //        string monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.SendMonth) + " " + m.SendYear;
        //        monthName += m.Charged ? "" : " (" + Resources.Resources.FreeOfCharge + ")";

        //        if(costPrice != null)
        //        {
        //            price = costPrice.Price;
        //        }
        //        else
        //        {
        //            price = 0.00d;
        //        }

        //        viewModel.Add(new SentMessageReportViewModel()
        //        {
        //            SendYear = m.SendYear,
        //            SendMonth = m.SendMonth,
        //            SendMonthName = monthName,
        //            NumberOfDeliveredMessages = m.NumberOfDeliveredMessages,
        //            NumberOfSentSMSes = m.NumberOfSentSMSes,
        //            NumberTypeName = db.NumberType.Find(m.NumberTypeID).Name,
        //            Cost = price * (m.Charged ? m.NumberOfDeliveredMessagesLength : 0),
        //            Charged = m.Charged
        //        });
        //    }

        //    return View(viewModel);
        //}

        public ActionResult SentSmsDetail(int clientId, int year, int month, bool charged)
        {
            logger.SetControllerAction("ReportController", "SentSmsDetail");
            logger.Info("Get client with id: " + clientId.ToString());
            var client = db.Client.Find(clientId);

            if (client == null)
            {
                logger.Info("Client not found");
                throw new HttpException(404, "Not found");
            }

            ViewBag.ClientName = client.Name;

            int clientID = clientId;
            logger.Info("Generate report for year: " + year.ToString() + " and month: " + month.ToString() + " and charged: " + charged);
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
                                               select nRpt.NumberType.NumberTypeID).FirstOrDefault()
                         }).ToList();

            List<SentMessageDetailReportViewModel> viewModel = new List<SentMessageDetailReportViewModel>();

            foreach (var m in model)
            {
                DateTime SendDate = new DateTime(m.SendYear, m.SendMonth, 1);
                double price = 0.00d;
                var costPrice = db.MessageCost
                        .Where(cost => cost.NumberTypeID == m.NumberTypeID && ((cost.StartDate <= SendDate && cost.EndDate >= SendDate) || (cost.StartDate <= SendDate && cost.EndDate == null))
                        && cost.NumberOfMessagesFrom <= m.MessageLengthOfDeliveredMessages && cost.NumberOfMessagesTo >= m.MessageLengthOfDeliveredMessages)
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
                    NumberOfDeliveredMessagesLength = m.MessageLengthOfDeliveredMessages,
                    PriceOfDeliveredMessagesLength = m.MessageLengthOfDeliveredMessages * price * (charged == true ? 1 : 0) 
                });
            }

            return View(viewModel);
        }

        //public ActionResult SentSMSJobReport(int id)
        //{
        //    var client = db.Client.Find(id);

        //    if (client == null)
        //    {
        //        throw new HttpException(404, "Not found");
        //    }

        //    ViewBag.ClientName = client.Name;

        //    int clientID = id;

        //    ViewBag.ClientId = clientID;

        //    // Paginacija
        //    int pageSize = 10000; // Broj linija po stranici
        //    int pageNumber = 0;
        //    List<SentSMSJobReportViewModel> model = new List<SentSMSJobReportViewModel>();

        //    while (true)
        //    {
        //        var messageNumbers = db.Numbers.Where(n => n.ClientID == clientID)
        //            .Join(db.MessagesNumbers,
        //                nums => nums.NumberID,
        //                mesnum => mesnum.NumberID,
        //                (nums, mesnum) => new { Nums = nums, Mesnums = mesnum })
        //            .Join(db.Message,
        //                mesnum => mesnum.Mesnums.MessageID,
        //                mess => mess.MessageID,
        //                (mesnum, mess) => new { mesnum.Nums, mesnum.Mesnums, Mess = mess })
        //            .GroupJoin(db.ScheduledSms,
        //                mess => mess.Mess.MessageID,
        //                sch => sch.MessageID,
        //                (mess, sch) => new { mess.Mess, mess.Mesnums, SchSms = sch.FirstOrDefault() })
        //            .Select(mesnum => new
        //            {
        //                ID = mesnum.Mesnums.MessageID,
        //                NumberID = mesnum.Mesnums.NumberID,
        //                NumberTypeID = mesnum.Mesnums.NumberTypeID,
        //                Alphanumeric = mesnum.Mess.Sender,
        //                Message = mesnum.Mess.MessageText,
        //                SendDate = mesnum.Mess.SendDate,
        //                User = mesnum.Mess.User.UserName,
        //                Status = mesnum.Mess.Status,
        //                SmsSchUser = mesnum.SchSms.User.UserName.ToString(),
        //                SmsSchDate = mesnum.SchSms.CancelDate
        //            })
        //            .OrderBy(mn => mn.ID)
        //            .Skip(pageNumber * pageSize)
        //            .Take(pageSize)
        //            .AsEnumerable()
        //            .ToList();

        //        if (!messageNumbers.Any())
        //        {
        //            break;
        //        }

        //        var numType = messageNumbers
        //            .GroupBy(mn => new { mn.ID, mn.NumberTypeID })
        //            .Select(mn => new { ID = mn.Key.ID, NumberTypeID = mn.Key.NumberTypeID, Count = mn.Count() })
        //            .ToList();

        //        foreach (var m in messageNumbers.GroupBy(mn => mn.ID))
        //        {
        //            var message = messageNumbers.Where(mn => mn.ID == m.Key).FirstOrDefault();
        //            var vpnCount = numType.Find(nt => nt.ID == m.Key && (NumberType)nt.NumberTypeID == NumberType.VPN);
        //            var inMtsCount = numType.Find(nt => nt.ID == m.Key && (NumberType)nt.NumberTypeID == NumberType.U_MTS);
        //            var outMtsCount = numType.Find(nt => nt.ID == m.Key && (NumberType)nt.NumberTypeID == NumberType.VAN_MTS);

        //            string status = GetStatus(message.Status);

        //            var note = "";
        //            if (status == Resources.Resources.ScheduledSendingCanceled)
        //                note = m.Select(sch => sch.SmsSchUser).FirstOrDefault() + " | " + m.Select(sch => sch.SmsSchDate).FirstOrDefault();

        //            model.Add(new SentSMSJobReportViewModel()
        //            {
        //                ID = m.Key,
        //                Alphanumeric = message.Alphanumeric,
        //                Message = message.Message,
        //                VPN = vpnCount == null ? 0 : vpnCount.Count,
        //                InMTS = inMtsCount == null ? 0 : inMtsCount.Count,
        //                OutMTS = outMtsCount == null ? 0 : outMtsCount.Count,
        //                SendDate = message.SendDate,
        //                User = message.User,
        //                Status = status,
        //                CanceledBy = note
        //            });
        //        }

        //        pageNumber++;
        //    }

        //    return View(model.OrderByDescending(m => m.ID));
        //}

        public ActionResult SentSMSJobReport(int id)
        {
            var client = db.Client.Find(id);

            if (client == null)
            {
                throw new HttpException(404, "Not found");
            }

            ViewBag.ClientName = client.Name;
            int clientID = id;
            ViewBag.ClientId = clientID;

            // Definišemo datum pre šest meseci
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
        public ActionResult SentSMSJobDetailReport(int clientId, int messageId)
        {
            var client = db.Client.Find(clientId);

            if (client == null)
            {
                throw new HttpException(404, "Not found");
            }

            ViewBag.ClientName = client.Name;

            int clientID = clientId;

            var checkUser = db.MessagesNumbers.Where(mn => mn.MessageID == messageId && mn.Message.User.ClientID == clientID).FirstOrDefault();

            if (checkUser == null)
            {
                throw new HttpException(400, "Bad Request");
            }
            
            var messageNumbers = db.MessagesNumbers
                .Where(mn => mn.MessageID == messageId)
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

        private string GetStatus(int status)
        {
            switch (status)
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
    }
    
}