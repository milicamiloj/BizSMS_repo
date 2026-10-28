#nullable disable
using BizSMS.Web.Data;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Web.Services
{
    // Pozadinski posao slanja kampanjskog SMS-a. Verni port legacy Helpers/SendSMS.StartSendSMS
    // (Hangfire job). Koristi se na dva načina:
    //   - trenutno slanje: HomeApiController ga poziva SINHRONO (await) u okviru request scope-a;
    //   - zakazano slanje: BackgroundJob.Schedule<SmsDispatchJob>(...) — Hangfire ga razrešava iz
    //     DI kontejnera u sopstvenom scope-u (sopstveni ApplicationDbContext).
    public class SmsDispatchJob
    {
        private readonly ApplicationDbContext db;
        private readonly ISmsSender _sms;
        private readonly IConfiguration _config;
        private readonly ReceiptReq _receiptReq;
        private readonly Logger logger = new Logger();

        public SmsDispatchJob(ApplicationDbContext db, ISmsSender sms, IConfiguration config, ReceiptReq receiptReq)
        {
            this.db = db;
            _sms = sms;
            _config = config;
            _receiptReq = receiptReq;
        }

        // Šalje sve još neposlate brojeve poruke preko SDP-a. Jedan pokušaj (kao legacy
        // [AutomaticRetry(Attempts = 1)]). clientID se koristi za formiranje teksta odjave.
        [Hangfire.AutomaticRetry(Attempts = 1)]
        public async Task StartSendSMS(int messageId, string alphanumeric, int clientID)
        {
            logger.SetControllerAction("SmsDispatchJob", "StartSendSMS");

            var message = db.Message.Find(messageId);
            if (message == null)
            {
                logger.Error("Poruka ne postoji: " + messageId);
                return;
            }

            // Uslovni UPDATE (par TryCancelScheduledMessage-u): u Processing prelazi samo poruka koja nije
            // otkazana ni završena, pa otkazivanje i početak slanja ne mogu oba da uspeju. Processing ostaje
            // dozvoljen radi Hangfire retry-ja (šalju se samo još neposlati brojevi).
            //int claimed = db.Message
            //    .Where(m => m.MessageID == messageId
            //        && m.Status != (int)MessageStatus.ScheduledSendingCanceled
            //        && m.Status != (int)MessageStatus.Finished)
            //    .ExecuteUpdate(s => s.SetProperty(m => m.Status, (int)MessageStatus.Processing));
            //if (claimed == 0)
            //{
            //    logger.Info("Poruka " + messageId + " je otkazana ili već poslata — slanje se preskače.");
            //    return;
            //}

            // Otkazana ili već završena poruka se preskače (idempotentnost pri Hangfire retry-ju).
            if (message.Status == (int)MessageStatus.ScheduledSendingCanceled || message.Status == (int)MessageStatus.Finished)
                return;

            message.Status = (int)MessageStatus.Processing;
            db.SaveChanges();

            var clientZeros = clientID.ToString("00000");
            var unsubMts = _config["Unsubscribe:TextMtsFirstPart"] + clientZeros + _config["Unsubscribe:TextMtsLastPart"];
            var unsubNonMts = _config["Unsubscribe:TextNotInMtsFirstPart"] + clientZeros + _config["Unsubscribe:TextNotInMtsLastPart"];

            var messageNumbers = db.MessagesNumbers
                .Where(mn => mn.MessageID == messageId && !mn.Sent)
                .ToList();

            foreach (var mn in messageNumbers)
            {
                var number = mn.NumbersModel; // lazy loading proxy
                string text = message.MessageText;
                if (number.NumberTypeID == (int)NumberType.U_MTS)
                    text = message.MessageText + Environment.NewLine + unsubMts;
                else if (number.NumberTypeID == (int)NumberType.VAN_MTS)
                    text = message.MessageText + Environment.NewLine + unsubNonMts;

                // Jedinstven correlator po redu (PK je Message_ID + Number_ID) — po njemu
                // notifySmsDeliveryReceipt (DLR) pronalazi i ažurira baš ovaj red.
                string correlator = mn.MessageID + "-" + mn.NumberID;

                string msgId;
                try
                {
                    msgId = await _sms.SendAsync(number.Number, text, alphanumeric, _receiptReq.Endpoint, correlator);
                }
                catch (InvalidOperationException)
                {
                    // SDP nije konfigurisan -> jasna greška (trenutno slanje: 500 pozivaocu;
                    // zakazano slanje: Hangfire označava posao kao neuspeo).
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Error("SDP slanje nije uspelo za broj " + Logger.MaskMsisdn(number.Number), ex);
                    msgId = "-1";
                }

                mn.SendDate = DateTime.Now;
                mn.SendSMSID = msgId;
                mn.Correlator = correlator;
                mn.Sent = msgId != "-1";
                if (!mn.Sent)
                    mn.Charged = false;
                db.SaveChanges();
            }

            message.Status = (int)MessageStatus.Finished;
            db.SaveChanges();
        }
    }
}
