#nullable disable
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BizSMS.Web.Data;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using BizSMS.Web.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Web.Controllers.Api
{
    // Zamena za legacy BizSMS.Controllers.API.HomeController (tok slanja SMS-a).
    // Trenutno slanje je SINHRONO preko SDP-a (odluka: bolja povratna informacija za male pošiljke);
    // zakazano slanje (ScheduledDateTime) ide preko Hangfire-a (BackgroundJob.Schedule, §9.2).
    // Persistencija (Message/MessageNumber), logika naplate i tekst odjave su verni legacy-ju.
    [ApiController]
    [Authorize(Roles = "Client,User")]
    [Route("api/Home")]
    public class HomeApiController : ControllerBase
    {
        private readonly ApplicationDbContext db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISmsSender _sms;
        private readonly IConfiguration _config;
        private readonly SmsDispatchJob _dispatchJob;
        private readonly Logger logger = new Logger();

        private readonly ReceiptReq _receiptReq;

        public HomeApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
            ISmsSender sms, IConfiguration config, ReceiptReq receiptReq, SmsDispatchJob dispatchJob)
        {
            this.db = db;
            _userManager = userManager;
            _sms = sms;
            _config = config;
            _receiptReq = receiptReq;
            _dispatchJob = dispatchJob;
        }

        // ClientID iz claim-a (AppClaimsPrincipalFactory) — bez dodatnog upita ka bazi.
        private int CurrentClientId => int.Parse(User.FindFirstValue("ClientID") ?? "0");

        // Hangfire je ožičen samo ako je HangfireDB stvarno konfigurisan (ista provera kao u Program.cs).
        private bool HangfireConfigured()
        {
            var cs = _config.GetConnectionString("HangfireDB");
            return !string.IsNullOrWhiteSpace(cs) && !cs.Contains("__");
        }

        // GET /api/Home/GetGroups — grupe klijenta za dropdown (PascalCase: Text/Value/Default).
        [HttpGet("GetGroups")]
        public IActionResult GetGroups()
        {
            logger.SetControllerAction("HomeApiController", "GetGroups");
            try
            {
                int clientID = CurrentClientId;
                var groups = db.Group
                    .Where(g => g.ClientID == clientID)
                    .Select(g => new { Text = g.Name, Value = g.GroupID, Default = g.Default })
                    .ToList();
                return Ok(groups);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // GET /api/Home/GetNumbers/{id} — brojevi grupe dozvoljeni za slanje.
        [HttpGet("GetNumbers/{id?}")]
        public IActionResult GetNumbers(int? id)
        {
            logger.SetControllerAction("HomeApiController", "GetNumbers");
            if (id == null)
                return NotFound();

            try
            {
                var gnumbers = db.GroupNumbers
                    .Where(gn => gn.GroupID == id && gn.Numbers.SendAllowed && gn.Numbers.Active)
                    .Select(gn => new { NumberID = gn.Numbers.NumberID, Number = gn.Numbers.Number, Name = gn.Numbers.Name })
                    .OrderBy(n => n.Number)
                    .ToList();
                return Ok(gnumbers);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // GET /api/Home/GetAlphanumerics — svi alfanumerici klijenta.
        [HttpGet("GetAlphanumerics")]
        public IActionResult GetAlphanumerics()
        {
            logger.SetControllerAction("HomeApiController", "GetAlphanumerics");
            try
            {
                int clientID = CurrentClientId;
                var alphanumerics = db.Alphanumeric
                    .Where(a => a.ClientID == clientID)
                    .Select(a => new { Text = a.Alphanumeric, Value = a.AlphanumericID })
                    .ToList();

                if (alphanumerics.Count > 0)
                    return Ok(alphanumerics);

                logger.Error("Klijent nema definisanih alfanumerika.");
                return StatusCode(StatusCodes.Status409Conflict, "Nema definisanih pošiljalaca (alfanumerika).");
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // GET /api/Home/GetTestNumber — broj telefona ulogovanog korisnika (za test SMS).
        [HttpGet("GetTestNumber")]
        public async Task<IActionResult> GetTestNumber()
        {
            logger.SetControllerAction("HomeApiController", "GetTestNumber");
            try
            {
                var user = await _userManager.GetUserAsync(User);
                return Ok(user.PhoneNumber);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // GET /api/Home/GetUnsubTextMts — tekst odjave za U MTS brojeve.
        [HttpGet("GetUnsubTextMts")]
        public IActionResult GetUnsubTextMts()
        {
            logger.SetControllerAction("HomeApiController", "GetUnsubTextMts");
            try
            {
                var z = CurrentClientId.ToString("00000");
                var text = _config["Unsubscribe:TextMtsFirstPart"] + z + _config["Unsubscribe:TextMtsLastPart"];
                return Ok(text);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // GET /api/Home/GetUnsubtextNotInMts — tekst odjave za VAN MTS brojeve.
        [HttpGet("GetUnsubtextNotInMts")]
        public IActionResult GetUnsubtextNotInMts()
        {
            logger.SetControllerAction("HomeApiController", "GetUnsubtextNotInMts");
            try
            {
                var z = CurrentClientId.ToString("00000");
                var text = _config["Unsubscribe:TextNotInMtsFirstPart"] + z + _config["Unsubscribe:TextNotInMtsLastPart"];
                return Ok(text);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // GET /api/Home/GetMessageStatus/{id} — status poruke (koristi SentSMSJobReport pre otkazivanja).
        [HttpGet("GetMessageStatus/{id}")]
        public IActionResult GetMessageStatus(int id)
        {
            logger.SetControllerAction("HomeApiController", "GetMessageStatus");
            try
            {
                var message = db.Message.Find(id);
                if (message == null)
                    return NotFound();
                return Ok(message.Status);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // POST /api/Home/CheckNonMtsGetAlphanums — bira alfanumerike zavisno od toga da li ima VAN MTS brojeva.
        [HttpPost("CheckNonMtsGetAlphanums")]
        public IActionResult CheckNonMtsGetAlphanums([FromBody] SendingNumbersCheck data)
        {
            logger.SetControllerAction("HomeApiController", "CheckNonMtsGetAlphanums");
            try
            {
                int clientID = CurrentClientId;

                // Pojedinačno slanje izabranih brojeva.
                if (data.NumbersToCheck != null)
                {
                    bool anyNonMts = db.Numbers.Any(n => data.NumbersToCheck.Contains(n.NumberID)
                        && n.NumberTypeID == (int)NumberType.VAN_MTS && n.Active && n.SendAllowed);
                    return anyNonMts ? GetNonMtsAlphanums(clientID) : GetAlphanumerics();
                }

                // Grupno slanje.
                if (data.GroupId != null)
                {
                    bool nonMts = db.GroupNumbers
                        .Where(gn => gn.GroupID == data.GroupId)
                        .Any(gn => gn.Numbers.ClientID == clientID
                            && gn.Numbers.NumberTypeID == (int)NumberType.VAN_MTS
                            && gn.Numbers.Active && gn.Numbers.SendAllowed);
                    return nonMts ? GetNonMtsAlphanums(clientID) : GetAlphanumerics();
                }

                throw new Exception("Nije prosleđen ni GroupId ni lista brojeva.");
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // POST /api/Home/SendTestSMS — šalje test poruku na broj ulogovanog korisnika (SINHRONO).
        [HttpPost("SendTestSMS")]
        public async Task<IActionResult> SendTestSMS([FromBody] TestSMSData data)
        {
            logger.SetControllerAction("HomeApiController", "SendTestSMS");
            try
            {
                var user = await _userManager.GetUserAsync(User);
                int clientID = user.ClientID;

                var numberdb = user.PhoneNumber;
                if (string.IsNullOrEmpty(numberdb))
                {
                    logger.Info("Korisnik nema broj telefona u bazi.");
                    return NotFound();
                }
                numberdb = "381" + numberdb.Remove(0, 1).Trim();

                // Alfanumerik mora pripadati klijentu.
                bool alphaOk = db.Alphanumeric.Any(a => a.ClientID == clientID && a.Alphanumeric == data.Alphanumeric);
                if (!alphaOk)
                {
                    logger.Info("Alfanumerik sa kojeg se šalje test poruka ne postoji u bazi klijenta.");
                    return NotFound();
                }

                string sendToNumber = "381" + data.PhoneNumber.Remove(0, 1).Trim();
                if (numberdb != sendToNumber)
                {
                    logger.Info("Broj na koji se šalje test poruka nije isti kao u bazi klijenta.");
                    return NotFound();
                }

                var message = new MessageModel
                {
                    MessageText = data.Message,
                    Sender = data.Alphanumeric,
                    MessageLength = Convert.ToInt32(data.MessageLength),
                    SendDate = DateTime.Now,
                    UserID = user.Id,
                    Test = true,
                    InsertDate = DateTime.Now
                };
                db.Message.Add(message);

                // Numerički sender (06...) -> 381 format (legacy paritet).
                var sender = data.Alphanumeric;
                if (!string.IsNullOrEmpty(sender) && Regex.Match(sender, @"^(06\d{7,8})").Success)
                    sender = "381" + sender.Remove(0, 1).Trim();

                await _sms.SendAsync(data.PhoneNumber, data.Message, sender, _receiptReq.Endpoint, _receiptReq.Correlator);
                db.SaveChanges();

                logger.Info("Test SMS poslat na broj: " + sendToNumber);
                return Ok(new { message = "OK" });
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // POST /api/Home/SendSMS — slanje na izabrane brojeve. INTERIM: sinhrono; zakazivanje odloženo.
        [HttpPost("SendSMS")]
        public async Task<IActionResult> SendSMS([FromBody] SMSData data)
        {
            logger.SetControllerAction("HomeApiController", "SendSMS");

            var user = await _userManager.GetUserAsync(User);
            int clientID = user.ClientID;

            if (data.PhoneNumbers == null || data.PhoneNumbers.Count < 1)
            {
                logger.Info("Greška: nema izabranih brojeva.");
                return BadRequest("Nema izabranih brojeva.");
            }

            string check = CheckData(clientID, data);
            if (check != "OK")
                return BadRequest(check);

            // Zakazano slanje zahteva ožičen Hangfire (HangfireDB). Ako nije konfigurisan — jasna poruka.
            if (!string.IsNullOrEmpty(data.ScheduledDateTime) && !HangfireConfigured())
            {
                logger.Info("Zakazivanje slanja zatraženo, ali HangfireDB nije konfigurisan.");
                return BadRequest("Zakazivanje slanja nije dostupno (HangfireDB nije konfigurisan). Moguće je samo trenutno slanje.");
            }

            try
            {
                bool chargeMessage = true;
                bool sendingNumbersContainNonVpn = true;

                if (!data.VpnGroupSending)
                    sendingNumbersContainNonVpn = db.Numbers.Any(n => data.PhoneNumbers.Contains(n.NumberID) && n.NumberTypeID != 1);

                if (data.VpnGroupSending || !sendingNumbersContainNonVpn)
                {
                    // Prvih 5 poruka u mesecu je besplatno (kada u bazi već ima > 4 nenaplaćene).
                    chargeMessage = db.Message.Count(m => m.User.ClientID == clientID
                        && !m.Test
                        && m.SendDate.Year == DateTime.Now.Year
                        && m.SendDate.Month == DateTime.Now.Month
                        && !m.Charged
                        && (m.Status == (int)MessageStatus.Finished || m.Status == (int)MessageStatus.Scheduled)) > 4;
                }

                int messageLength = GetMessageLength(data.Message);

                var message = new MessageModel
                {
                    MessageText = data.Message,
                    Sender = data.Alphanumeric,
                    MessageLength = messageLength,
                    SendDate = DateTime.Now,
                    UserID = user.Id,
                    Test = false,
                    Status = (int)MessageStatus.Queued,
                    InsertDate = DateTime.Now,
                    Charged = chargeMessage
                };
                db.Message.Add(message);
                db.SaveChanges();

                int messageId = message.MessageID;

                var clientZeros = clientID.ToString("00000");
                var unsubMts = _config["Unsubscribe:TextMtsFirstPart"] + clientZeros + _config["Unsubscribe:TextMtsLastPart"];
                var unsubNonMts = _config["Unsubscribe:TextNotInMtsFirstPart"] + clientZeros + _config["Unsubscribe:TextNotInMtsLastPart"];

                // Dužina za NumberType 1 (VPN) i 2 (U MTS) == messageLength; za 3 (VAN MTS) uključuje tekst odjave.
                int messageLengthNT3 = GetMessageLength(data.Message + Environment.NewLine + unsubNonMts);

                int count = 1;
                foreach (var numberId in data.PhoneNumbers)
                {
                    var number = db.Numbers.FirstOrDefault(n =>
                        n.NumberID == numberId && n.ClientID == clientID && n.SendAllowed && n.Active);
                    if (number == null)
                        continue;

                    int numberType = number.NumberTypeID;
                    // Naplaćuje se svakom broju osim VPN (type 1) kada mesečni besplatni kvota nije ispucana.
                    bool isMessageCharged = !(numberType == 1 && chargeMessage == false);

                    var mn = new MessageNumberModel
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
                        MessageLengthNT = numberType == 3 ? messageLengthNT3 : messageLength
                    };
                    db.BulkInsert(mn, count++, 100);
                }
                db.SaveChanges();

                // Zakazano slanje: Hangfire posao u budućnosti (verni port legacy BackgroundJob.Schedule).
                if (!string.IsNullOrEmpty(data.ScheduledDateTime))
                {
                    var when = DateTime.Parse(data.ScheduledDateTime, CultureInfo.InvariantCulture);
                    message.Status = (int)MessageStatus.Scheduled;
                    db.SaveChanges();

                    var hangfireId = BackgroundJob.Schedule<SmsDispatchJob>(
                        j => j.StartSendSMS(messageId, data.Alphanumeric, clientID), when);

                    db.ScheduledSms.Add(new ScheduledSmsModel
                    {
                        HangfireID = hangfireId,
                        MessageID = messageId,
                        UserInsert = user.Id,
                        InsertDate = DateTime.Now
                    });
                    db.SaveChanges();

                    logger.Info("SMS sa messageId: " + messageId + " zakazan za " + when + " (Hangfire posao " + hangfireId + ").");
                    return Ok(new { message = "OK" });
                }

                // Trenutno slanje: sinhrono (odluka korisnika). Isti posao koji izvršava i Hangfire,
                // ali pozvan direktno u request scope-u (deli isti DbContext) radi povratne informacije pozivaocu.
                await _dispatchJob.StartSendSMS(messageId, data.Alphanumeric, clientID);

                logger.Info("SMS sa messageId: " + messageId + " obrađen (sinhrono slanje).");
                return Ok(new { message = "OK" });
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message + (ex.InnerException != null ? ": " + ex.InnerException.Message : ""));
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        // POST /api/Home/SendGroupSMS — razreši brojeve grupe pa prosledi SendSMS-u.
        [HttpPost("SendGroupSMS")]
        public async Task<IActionResult> SendGroupSMS([FromBody] GroupSMSData data)
        {
            logger.SetControllerAction("HomeApiController", "SendGroupSMS");
            logger.Info("Pokrenuto slanje SMS-a grupi sa id: " + data.GroupId);

            var group = db.Group.Find(data.GroupId);
            if (group == null)
                return BadRequest("Grupa ne postoji.");

            data.VpnGroupSending = group.Name == NumberType.VPN.ToString();

            data.PhoneNumbers = db.GroupNumbers
                .Where(gn => gn.GroupID == data.GroupId && gn.Numbers.SendAllowed && gn.Numbers.Active)
                .Select(gn => gn.NumberID)
                .ToList();

            return await SendSMS(data);
        }

        // POST /api/Home/CancelScheduledSMS/{id} — otkazuje zakazano slanje: briše Hangfire posao i ažurira stanje u bazi.
        [HttpPost("CancelScheduledSMS/{id}")]
        public IActionResult CancelScheduledSMS(int id)
        {
            logger.SetControllerAction("HomeApiController", "CancelScheduledSMS");
            try
            {
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var message = db.Message.Find(id);
                if (message == null)
                    return NotFound();

                // Zaštita od IDOR-a: klijent sme da otkaže samo poruku svog klijenta.
                // Vraćamo NotFound (ne 403) da ne otkrivamo postojanje tuđih poruka.
                if (message.User.ClientID != CurrentClientId)
                    return NotFound();

                if (message.Status == (int)MessageStatus.Finished)
                    return Ok("Poruka je već poslata!");

                message.Status = (int)MessageStatus.ScheduledSendingCanceled;
                // Otkazana poruka ne sme biti naplaćena.
                message.Charged = false;

                var scheduled = db.ScheduledSms.FirstOrDefault(s => s.MessageID == message.MessageID);
                if (scheduled != null)
                {
                    // Obriši zakazani Hangfire posao da se ne izvrši (verni port legacy BackgroundJob.Delete).
                    if (!string.IsNullOrEmpty(scheduled.HangfireID))
                    {
                        try { BackgroundJob.Delete(scheduled.HangfireID); }
                        catch (Exception ex) { logger.Error("Brisanje Hangfire posla nije uspelo: " + ex.Message); }
                    }
                    scheduled.UserID = userId;
                    scheduled.CancelDate = DateTime.Now;
                }

                db.SaveChanges();
                logger.Info("Zakazani SMS sa messageId: " + id + " otkazan.");
                return Ok(new { message = "OK" });
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // --- Pomoćne metode ---

        // Vraća numeričke alfanumerike klijenta (potrebni za slanje na VAN MTS brojeve).
        private IActionResult GetNonMtsAlphanums(int clientID)
        {
            logger.SetControllerAction("HomeApiController", "GetNonMtsAlphanums");
            try
            {
                var all = db.Alphanumeric.Where(a => a.ClientID == clientID).ToList();
                var numeric = all
                    .Where(a => !string.IsNullOrEmpty(a.Alphanumeric) && Regex.Match(a.Alphanumeric, @"^(06\d{7,8})").Success)
                    .Select(a => new { Text = a.Alphanumeric, Value = a.AlphanumericID })
                    .ToList();

                if (numeric.Count > 0)
                    return Ok(numeric);

                logger.Error("Nedostaje numerički pošiljalac potreban za slanje na brojeve van MTS mreže.");
                return StatusCode(StatusCodes.Status409Conflict,
                    "Ne postoji numerički pošiljalac potreban za slanje na brojeve van MTS mreže. Kontaktirajte administratora.");
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
                if (ch > 127)
                    return (int)Math.Ceiling(message.Length / 66d);
            }
            return (int)Math.Ceiling(message.Length / 160d);
        }

        // Svi prosleđeni brojevi moraju pripadati klijentu (i biti aktivni + dozvoljeni za slanje).
        private bool CheckNumbers(int clientID, List<int> phoneNumbers)
        {
            var numbers = db.Numbers
                .Where(n => n.ClientID == clientID && n.SendAllowed && n.Active)
                .Select(n => n.NumberID)
                .ToList();

            return !phoneNumbers.Except(numbers).Any();
        }

        private string CheckData(int clientID, SMSData data)
        {
            if (data.Alphanumeric == null ||
                !db.Alphanumeric.Any(a => a.Alphanumeric == data.Alphanumeric && a.ClientID == clientID))
            {
                logger.Error("Pogrešan alfanumerik.");
                return "Pošiljalac (alfanumerik) nije validan.";
            }

            if (!CheckNumbers(clientID, data.PhoneNumbers))
            {
                logger.Error("Ne pripadaju svi brojevi ulogovanom klijentu.");
                return "Problem sa izabranim brojevima.";
            }

            return "OK";
        }
    }
}
