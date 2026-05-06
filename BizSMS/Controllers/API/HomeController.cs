using BizSMS.Attributes;
using BizSMS.Helpers;
using BizSMS.Models;
using Hangfire;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Http;
using System.Web.Http.Cors;

namespace BizSMS.Controllers.API
{
    [AuthorizeApiUser]
    public class HomeController : ApiController
    {
        Logger logger = new Logger();

        [HttpGet]
        public IHttpActionResult GetGroups()
        {
            logger.SetControllerAction("HomeController:ApiController", "GetGroups");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();

                var userID = User.Identity.GetUserId();
                var clientID = db.Users.Where(user => user.Id == userID).FirstOrDefault().ClientID;

                var groups = db.Group
                    .Where(g => g.ClientID == clientID)
                    .Select(g => new
                    {
                        Text = g.Name,
                        Value = g.GroupID,
                        Default = g.Default
                    })
                    .ToList();

                logger.Info("Groups are returned from api");
                return Ok(groups);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }

        }

        [HttpGet]
        public IHttpActionResult GetNumbers(string id)
        {
            logger.SetControllerAction("HomeController:ApiController", "GetNumbers");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();

                var gnumbers = (from numbers in db.GroupNumbers
                                where numbers.GroupID.ToString() == id && numbers.Numbers.SendAllowed && numbers.Numbers.Active
                                select new
                                {
                                    NumberID = numbers.Numbers.NumberID,
                                    Number = numbers.Numbers.Number,
                                    Name = numbers.Numbers.Name
                                }).OrderBy(n => n.Number).ToList();

                logger.Info("Numbers for selected group id: " + id + ", has been returned from api");
                return Ok(gnumbers);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        [HttpGet]
        public IHttpActionResult GetAlphanumerics()
        {
            logger.SetControllerAction("HomeController:ApiController", "GetAlphanumerics");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();

                var userID = User.Identity.GetUserId();
                var clientID = db.Users.Where(u => u.Id == userID).Select(u => u.ClientID).First();
                var alphanumerics = db.Alphanumeric
                    .Where(a => a.ClientID == clientID)
                    .Select(a =>
                    new
                    {
                        Text = a.Alphanumeric,
                        Value = a.AlphanumericID
                    });

                if (alphanumerics.Count() > 0)
                {
                    logger.Info("Alphanumerics for logged clientID: " + clientID + " is returned successfuly");
                    return Ok(alphanumerics);
                }
                else
                {
                    logger.Error("No Alphanumerics for logged client/user");
                    return Content(System.Net.HttpStatusCode.Ambiguous, "No Alphanumerics");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        [HttpGet]
        public IHttpActionResult GetTestNumber()
        {
            logger.SetControllerAction("HomeController:ApiController", "GetTestNumber");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();

                var userID = User.Identity.GetUserId();
                var number = db.Users.Where(u => u.Id == userID).Select(u => u.PhoneNumber).First();

                logger.Info("Test msisdn is returned from db");
                return Ok(number);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        [HttpPost]
        public IHttpActionResult SendTestSMS(TestSMSData data)
        {
            logger.SetControllerAction("HomeController:ApiController", "SendTestSMS");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();
                http2sms.http2sms h2s = new http2sms.http2sms();

                var userIDdb = User.Identity.GetUserId();
                var numberdb = db.Users.Where(u => u.Id == userIDdb).Select(u => u.PhoneNumber).FirstOrDefault();
                numberdb = "381" + numberdb.Remove(0, 1).Trim();

                var clientIDdb = db.Users.Find(userIDdb).ClientID;
                var alphanumericdb = db.Alphanumeric.Where(a => a.ClientID == clientIDdb).Select(a => a.Alphanumeric).ToList();
                int i = 0;
                int j = 0;

                foreach (var alpha in alphanumericdb)
                {
                    j++;
                    if (data.Alphanumeric != alpha)
                        i++;
                }

                if (i != j-1)
                {
                    logger.Info("Alphanumeric sa kojeg se salje poruka ne postoji u bazi klijenta!!");
                    return NotFound();
                }

                string sendToNumber = "381" + data.PhoneNumber.Remove(0, 1).Trim();
                string msgId = "-1";

                if (numberdb==null || numberdb!=sendToNumber)
                {
                    logger.Info("Broj na koji se šalje test poruka nije kao u bazi klijenta!!");
                    return NotFound();                   
                }

                MessageModel Message = new MessageModel()
                {
                    MessageText = data.Message,
                    Sender = data.Alphanumeric,
                    MessageLength = System.Convert.ToInt32(data.MessageLength),
                    SendDate = System.DateTime.Now,
                    UserID = User.Identity.GetUserId(),
                    Test = true,
                    InsertDate = DateTime.Now
                };

                db.Message.Add(Message);

                var phoneNumberFormat = @"^(06\d{7,8})";
                if (Regex.Match(data.Alphanumeric, phoneNumberFormat).Success)
                {
                    data.Alphanumeric = "381" + data.Alphanumeric.Remove(0, 1).Trim();
                }
                msgId = h2s.Send(data.Alphanumeric, new string[] { sendToNumber }, data.Message, "BizSMS", "conBizsms");

                db.SaveChanges();

                logger.Info("Test sms has been sent with messageId: " + msgId + " to number: " + sendToNumber);

                return Ok(new { message = "OK" });              
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }



        [HttpPost]
        [EnableCors(origins: "https://bizsms.telekom.rs", headers: "*", methods: "*")]
        public IHttpActionResult SendSMS(SMSData data)
        {
            logger.SetControllerAction("HomeController:ApiController", "SendSMS");

            ApplicationDbContext db = new ApplicationDbContext();
            //db.Database.Log += s => Debug.WriteLine(s);

            var userId = User.Identity.GetUserId();
            var clientID = db.Users.Find(userId).ClientID;

            logger.Info("Get user and client id: user- " + userId + " clientId- " + clientID.ToString());

            //data validation
            if (data.PhoneNumbers == null || data.PhoneNumbers.Count() < 1)
            {
                logger.Info("Error: No numbers, PhoneNumbers.Count() = " + data.PhoneNumbers.Count());
                return BadRequest("No numbers");
            }


            string checkNumbers = CheckData(db, clientID, data);
            logger.Info("Check numbers pass: " + checkNumbers);

            if (checkNumbers != "OK")
                return BadRequest(checkNumbers);

            if (data.ScheduledDateTime != null)
            {
                if (System.DateTime.Parse(data.ScheduledDateTime) < DateTime.Now)
                {
                    try
                    {
                        var cookie = Request.Headers.GetCookies();

                        Thread.CurrentThread.CurrentCulture = new CultureInfo(cookie[0]["_culture"].Value);
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo(cookie[0]["_culture"].Value);

                        if (cookie[0]["_culture"] == null || cookie[0]["_culture"].Value == "")
                        {
                            throw new Exception("LanguageError: cookie language not set");
                        }
                    }
                    catch
                    {
                        Thread.CurrentThread.CurrentCulture = new CultureInfo("sr");
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo("sr");
                    }

                    return BadRequest(String.Format(CultureInfo.CurrentCulture, Resources.Resources.Error + ": " + Resources.Resources.WrongDate));
                }
            }

            try
            {           
                bool chargeMessage = true;
                bool sendingNumbersContainNonVpn = true;

                if (!data.VpnGroupSending)
                {
                    sendingNumbersContainNonVpn = db.Numbers.Where(n => data.PhoneNumbers.Contains(n.NumberID) && n.NumberTypeID != 1).Any();
                    logger.Info("Destination numbers contain NON VPN: " + sendingNumbersContainNonVpn);
                }

                logger.Info("VPN group sending: " + data.VpnGroupSending);

                if (data.VpnGroupSending || !sendingNumbersContainNonVpn)
                {
                    //upit proverava broj vec poslatih besplatnih poruka u Message tabeli
                    chargeMessage = db.Message.Where(m => m.User.ClientID == clientID
                        && m.Test == false
                        && m.SendDate.Year == DateTime.Now.Year
                        && m.SendDate.Month == DateTime.Now.Month
                        && m.Charged == false
                        && (m.Status == 4 || m.Status == 2))
                        .Count() > 4; //kada salje 5. bespl poruku u bazi ce ih biti 4 (Count() = 4 -> chargeMessage = false)
                }

                logger.Info("Message is charged: " + chargeMessage);

                var messageLength = GetMessageLength(data.Message);

                MessageModel Message = new MessageModel()
                {
                    MessageText = data.Message,
                    Sender = data.Alphanumeric,
                    MessageLength = messageLength,
                    SendDate = data.ScheduledDateTime != null ? DateTime.Parse(data.ScheduledDateTime) : DateTime.Now,
                    UserID = userId,
                    Test = false,
                    Status = (int)MessageStatus.Queued,
                    InsertDate = DateTime.Now,
                    Charged = chargeMessage
                };

                db.Message.Add(Message);
                db.SaveChanges();

                logger.Info("Message saved successfully");

                string user = User.Identity.GetUserName();
                int messageId = Message.MessageID;

                logger.Info("MessageID: " + messageId.ToString());

                int count = 1;
                int numberType;

                //duzina poruke za numberType = 1 je messageLength
                //duzinu poruke za numberType = 2 je ista kao kod numberType = 1 jer se nece naplacivati dodatni tekst o odjavi
                var messageLengthNT2 = messageLength;
                //var messageLengthNT2 = GetMessageLength(data.Message + ConfigurationManager.AppSettings["unsubscribeTextMTS"] + clientID.ToString("00000"));

                //duzinu poruke za numberType = 3;
                var messageLengthNT3 = GetMessageLength(data.Message + ConfigurationManager.AppSettings["unsubTextNotInMtsFirstPart"] + clientID.ToString("00000") + ConfigurationManager.AppSettings["unsubTextNotInMtsLastPart"]);

                foreach (var numberId in data.PhoneNumbers)
                {
                    var Number = db.Numbers.Where(n =>
                    (n.NumberID == numberId) &
                    (n.ClientID == clientID) &
                    (n.SendAllowed) &
                    (n.Active)).FirstOrDefault();

                    numberType = Number.NumberTypeID;

                    //poruka ce biti naplacena svakom broju osim ako je broj VPN && nisu ispucane prvih 5 poruka
                    bool isMessageCharged = true;
                    if (numberType == 1 && chargeMessage == false)
                    {
                        isMessageCharged = false;
                    }

                    //logger.Info("Charge message: " + chargeMessage.ToString() + ", NumberID = " + numberId + ", NumberType = " + Number.NumberType.Name);

                    MessageNumberModel MessageNumber = new MessageNumberModel()
                    {
                        MessageID = messageId,
                        NumberID = numberId,
                        SendDate = DateTime.Now,
                        Delivered = 0,
                        SendSMSID = null,
                        Sent = false,
                        NumberTypeID = numberType,
                        InsertDate = DateTime.Now,
                        Charged = isMessageCharged,
                        MessageLengthNT = numberType == 1 ? messageLength : numberType == 2 ? messageLengthNT2 : messageLengthNT3
                    };

                    db.BulkInsert(MessageNumber, count++, 100);

                }

                db.SaveChanges();

                logger.Info("Numbers added to MessageNumber table for messageId: " + messageId.ToString());

                SendSMS sms = new SendSMS();
                if (data.ScheduledDateTime == null)
                {
                    //BackgroundJob.Enqueue(() => sms.StartSendSMS(user, data.Alphanumeric, messageId, clientID));
                    try
                    {
                        BackgroundJob.Enqueue(() => sms.StartSendSMS(user, data.Alphanumeric, messageId, clientID));
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Nangfire exception {ex}");
                    }

                    logger.Info("Message successfully enqued for send");
                }
                else
                {
                    Message.Status = (int)MessageStatus.Scheduled;
                    var hangfireId = BackgroundJob.Schedule(() => sms.StartSendSMS(user, data.Alphanumeric, messageId, clientID), DateTime.Parse(data.ScheduledDateTime));
                    ScheduledSmsModel ScheduledSms = new ScheduledSmsModel()
                    {
                        HangfireID = hangfireId,
                        MessageID = messageId,
                        UserInsert = userId,
                        InsertDate = DateTime.Now
                    };
                    db.ScheduledSms.Add(ScheduledSms);
                    db.SaveChanges();
                    logger.Info("Message successfully scheduled for send");
                }

                logger.Info("SMS with messageId: " + messageId.ToString() + " queued for send");

                return Ok(new { message = "OK" });
            }
            catch (System.Exception ex)
            {
                logger.Error(ex.Message + ": " + ex.InnerException.Message);
                return InternalServerError();
            }
        }

        [HttpPost]
        [EnableCors(origins: "https://bizsms.telekom.rs", headers: "*", methods: "*")]
        public IHttpActionResult SendGroupSMS(GroupSMSData data)
        {

            logger.SetControllerAction("HomeController:ApiController", "SendGroupSMS");
            logger.Info("Initiated sending SMS to group with id:" + data.GroupId);

            ApplicationDbContext db = new ApplicationDbContext();

            var sendingGroupIdName = db.Group.Find(data.GroupId).Name;

            if (sendingGroupIdName == NumberType.VPN.ToString())
            {
                data.VpnGroupSending = true;
            }
            else
            {
                data.VpnGroupSending = false;
            }

            List<int> numberIds = db.GroupNumbers
                                        .Where(gn => gn.GroupID == data.GroupId && gn.Numbers.SendAllowed && gn.Numbers.Active)
                                        .Select(groupNum => groupNum.NumberID)
                                        .ToList();

            data.PhoneNumbers = numberIds;

            return SendSMS(data);

        }

        [HttpPost]
        public IHttpActionResult CancelScheduledSMS(int id)
        {
            logger.SetControllerAction("HomeController:ApiController", "CancelScheduledSMS");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();

                var userCancelId = User.Identity.GetUserId();
                var clientID = db.Users.Find(userCancelId).ClientID;

                MessageModel Message = db.Message.Find(id);
                if (Message.Status == 4)
                {
                    return Ok("Message already sent!");
                }
                ScheduledSmsModel HangfireJob = db.ScheduledSms
                                        .Where(hj => hj.MessageID == Message.MessageID)
                                        .FirstOrDefault();

                BackgroundJob.Delete(HangfireJob.HangfireID);
                logger.Info("Canceling scheduled SMS with messageId: " + id + ", by clientId: " + clientID.ToString() + " and user: " + userCancelId);

                Message.Status = (int)MessageStatus.ScheduledSendingCanceled;
                //ukoliko je poruka uspesno otkazana charged mora biti false
                Message.Charged = false;
                db.Entry(Message).State = System.Data.Entity.EntityState.Modified;

                HangfireJob.UserID = userCancelId;
                HangfireJob.CancelDate = DateTime.Now;
                db.Entry(HangfireJob).State = System.Data.Entity.EntityState.Modified;

                db.SaveChanges();

                return Ok(new { message = "OK" });
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        private int GetMessageLength(string message)
        {
            foreach (char ch in message)
            {
                if ((int)ch > 127)
                    return (int)System.Math.Ceiling(message.Length / 66d);
            }
            return (int)System.Math.Ceiling(message.Length / 160d);
        }

        /// <summary>
        /// Check if all phone numbers exist in numbers
        /// </summary>
        /// <param name="db"></param>
        /// <param name="clientID"></param>
        /// <param name="phoneNumbers"></param>
        /// <returns></returns>
        public bool CheckNumbers(ApplicationDbContext db, int clientID, List<int> phoneNumbers)
        {
            List<int> Numbers = db.Numbers.Where(n =>
                (n.ClientID == clientID) &
                (n.SendAllowed) &
                (n.Active)).Select(n => n.NumberID).ToList();

            //Any() returns false if all phoneNumbers exist in Numbers
            return !phoneNumbers.Except(Numbers).Any();
        }

        private string CheckData(ApplicationDbContext db, int clientID, SMSData data)
        {
            if (data.Alphanumeric == null || db.Alphanumeric.Where(a => a.Alphanumeric == data.Alphanumeric).FirstOrDefault() == null)
            {
                logger.Error("Wrong alphanumeric");
                return "Wrong alphanumeric";
            }

            if (!CheckNumbers(db, clientID, data.PhoneNumbers))
            {
                logger.Error("Not all numbers belong to logged client");
                return "Problem with numbers";
            }

            return "OK";
        }

        [HttpGet]
        public IHttpActionResult GetUnsubTextMts()
        {
            logger.SetControllerAction("HomeController:ApiController", "GetUnsubTextMts");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();
                var userId = User.Identity.GetUserId();
                var clientID = db.Users.Find(userId).ClientID;
                var clientIDWithZeroes = clientID.ToString("00000");
                var unsubTextInMts = ConfigurationManager.AppSettings["unsubscribeTextMTSFirstPart"] + clientIDWithZeroes + ConfigurationManager.AppSettings["unsubscribeTextMTSLastPart"];
                logger.Info("Unsubscription text in MTS returned successfuly: " + unsubTextInMts);
                return Ok(unsubTextInMts);
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message);
                return NotFound();
            }

        }

        [HttpGet]
        public IHttpActionResult GetUnsubtextNotInMts()
        {
            logger.SetControllerAction("HomeController:ApiController", "GetUnsubtextNotInMts");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();
                var userId = User.Identity.GetUserId();
                var clientID = db.Users.Find(userId).ClientID;
                var clientIDWithZeroes = clientID.ToString("00000");
                var unsubTextNotInMts = ConfigurationManager.AppSettings["unsubTextNotInMtsFirstPart"] + clientIDWithZeroes + ConfigurationManager.AppSettings["unsubTextNotInMtsLastPart"];
                logger.Info("Unsubscription text NOT in MTS returned successfuly: " + unsubTextNotInMts);
                return Ok(unsubTextNotInMts);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        [HttpGet]
        public IHttpActionResult GetMessageStatus(int Id)
        {
            logger.SetControllerAction("HomeController:ApiController", "GetMessageStatus");
            try
            {
                ApplicationDbContext db = new ApplicationDbContext();
                var messageStatus = db.Message.Find(Id).Status;
                logger.Info("Message status for messageId: " + Id + ", returned successfuly: " + messageStatus);
                return Ok(messageStatus);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        [HttpPost]
        public IHttpActionResult CheckNonMtsGetAlphanums(SendingNumbersCheck data)
        {
            logger.SetControllerAction("HomeController:ApiController", "CheckNonMtsGetAlphanums");
            try
            {
                ApplicationDbContext db = new ApplicationDbContext();
                var userId = User.Identity.GetUserId();
                var clientID = db.Users.Find(userId).ClientID;

                //ako data.NumbersToCheck nije null znaci da je u pitanju pojedinacno slanje brojeva
                if (data.NumbersToCheck != null)
                {
                    foreach (var numberId in data.NumbersToCheck)
                    {
                        if (db.Numbers.Where(n => n.NumberID == numberId).Any(nt => nt.NumberTypeID == (int)NumberType.VAN_MTS && nt.Active && nt.SendAllowed))
                        {
                            //postoje brojevi van mts
                            logger.Info("Returning NON MTS alphanumerics for clientID: " + clientID);
                            return GetNonMtsAlphanums(clientID);
                        }
                    }
                    //svi brojevi su u mts
                    logger.Info("Returning MTS alphanumerics for clientID: " + clientID);
                    return GetAlphanumerics();
                }
                //grupno slanje brojeva
                else if (data.GroupId != null)
                {
                    var nonMts = db.Numbers.Where(n => n.ClientID == clientID)
                    .Join(db.GroupNumbers.Where(g => g.GroupID == data.GroupId),
                    nums => nums.NumberID,
                    gn => gn.NumberID,
                    (nums, gn) => new { Nums = nums, Groupnums = gn })
                    .Any(nt => nt.Nums.NumberTypeID == (int)NumberType.VAN_MTS && nt.Nums.Active && nt.Nums.SendAllowed);

                    //postoje brojevi van mts
                    if (nonMts)
                    {
                        logger.Info("Returning NON MTS alphanumerics for clientID: " + clientID);
                        return GetNonMtsAlphanums(clientID);
                    }

                    //svi brojevi su u mts
                    logger.Info("Returning MTS alphanumerics for clientID: " + clientID);
                    return GetAlphanumerics();
                }
                else
                {
                    throw new Exception("Doslo je do greske pri prosledjivanju Id grupe: " + data.GroupId + ", ili pojedinacnih brojeva: " + data.NumbersToCheck + ".");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        private IHttpActionResult GetNonMtsAlphanums(int clientID)
        {
            logger.SetControllerAction("HomeController:ApiController", "GetNonMtsAlphanums");

            try
            {
                var phoneNumberFormat = @"^(06\d{7,8})";
                ApplicationDbContext db = new ApplicationDbContext();

                var allAlphanumerics = db.Alphanumeric.Where(a => a.ClientID == clientID).Select(a => a);

                List<int> numericAlphanumId = new List<int>();

                foreach (var alphanumeric in allAlphanumerics)
                {
                    if (Regex.Match(alphanumeric.Alphanumeric, phoneNumberFormat).Success)
                    {
                        numericAlphanumId.Add(alphanumeric.AlphanumericID);
                    }
                }

                var numericAlphanumeric =
                    from alp in allAlphanumerics
                    from numAlp in numericAlphanumId
                    where numAlp == alp.AlphanumericID
                    select new
                    {
                        Text = alp.Alphanumeric,
                        Value = alp.AlphanumericID
                    };

                if (numericAlphanumeric.Count() > 0)
                {
                    logger.Info("NON MTS Alphanumerics = numeric for logged clientID: " + clientID + " returned successfuly");
                    return Ok(numericAlphanumeric);
                }
                //u slucaju da ne postoji numericki sender u bazi, to se loguje i nazad se salje poruka klijentu da kontaktira admina i formira takav sender
                else
                {
                    try
                    {
                        var cookie = Request.Headers.GetCookies();

                        Thread.CurrentThread.CurrentCulture = new CultureInfo(cookie[0]["_culture"].Value);
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo(cookie[0]["_culture"].Value);

                        if (cookie[0]["_culture"] == null || cookie[0]["_culture"].Value == "")
                        {
                            throw new Exception("LanguageError: cookie language not set");
                        }
                    }
                    catch
                    {
                        Thread.CurrentThread.CurrentCulture = new CultureInfo("sr");
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo("sr");
                    }
                    logger.Error("Missing Alphanumerics = numeric for logged client/user required for nonMts numbers sending");
                    return Content(System.Net.HttpStatusCode.Ambiguous, Resources.Resources.NoNumericSender);
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }
    }
}
