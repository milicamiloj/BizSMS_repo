using BizSMS.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace BizSMS.Helpers
{
    public class SendSMS
    {
        ApplicationDbContext db;
        http2sms.http2sms h2s;
        Logger logger = new Logger();

        public SendSMS()
        {
            db = new ApplicationDbContext();
            h2s = new http2sms.http2sms();
        }

        public SendSMS(ApplicationDbContext db)
        {
            this.db = db;
            h2s = new http2sms.http2sms();
        }

        [Hangfire.AutomaticRetry(Attempts = 1)]
        public void StartSendSMS(string username, string alphanumeric, int messageId, int clientID)
        {
            string msgId = "-1";
            
            var Message = db.Message.Find(messageId);

            //check if the message has been canceled or finished
            if (Message.Status == (int)MessageStatus.ScheduledSendingCanceled || Message.Status == (int)MessageStatus.Finished)
                return;

            Message.Status = (int)MessageStatus.Processing;
            db.Entry(Message).State = System.Data.Entity.EntityState.Modified;
            db.SaveChanges();

            var Numbers = Message.MessagesNumbers.Where(mn => mn.Sent == false).Select(mn => mn.NumbersModel).ToList();
            var clientIDWithZeroes = clientID.ToString("00000");

            //form unsubscribe message text
            var unsubscribeTextMTS = ConfigurationManager.AppSettings["unsubscribeTextMTSFirstPart"] + clientIDWithZeroes + ConfigurationManager.AppSettings["unsubscribeTextMTSLastPart"];
            var unsubscribeTextNotInMTS = ConfigurationManager.AppSettings["unsubTextNotInMtsFirstPart"] + clientIDWithZeroes + ConfigurationManager.AppSettings["unsubTextNotInMtsLastPart"];

            foreach (var number in Numbers)
            {
                string message = Message.MessageText;
                //attach unsubscribe text to messages to NumberType U_MTS and VAN_MTS numbers
                if (number.NumberTypeID == (int)NumberType.U_MTS)
                {
                    message = Message.MessageText + Environment.NewLine + unsubscribeTextMTS;
                }
                else if (number.NumberTypeID == (int)NumberType.VAN_MTS)
                {
                    message = Message.MessageText + Environment.NewLine + unsubscribeTextNotInMTS;
                }

                //send message
                msgId = Send(alphanumeric, message, h2s, number.Number);

                //update messageNumber table
                var MessageNumber = db.MessagesNumbers.Find(number.NumberID, messageId);
                MessageNumber.SendDate = DateTime.Now;
                MessageNumber.SendSMSID = msgId;
                MessageNumber.Sent = (msgId != "-1" ? true : false);

                //ukoliko se desi da poruka nije poslata na taj broj charged za taj broj mora biti 0
                if (MessageNumber.Sent == false)
                {
                    MessageNumber.Charged = false;
                }

                db.Entry(MessageNumber).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
            }

            Message.Status = (int)MessageStatus.Finished;

            db.SaveChanges();
        }

        private string Send(string alphanumeric, string message, http2sms.http2sms h2s, string number)
        {
            string msgId = "-1";

            var phoneNumberFormat = @"^(06\d{7,8})";
            if (Regex.Match(alphanumeric, phoneNumberFormat).Success)
            {
                alphanumeric = "381" + alphanumeric.Remove(0, 1).Trim();
            }

            string SendToNumber = "381" + number.Remove(0, 1).Trim();

            try
            {
                msgId = h2s.Send(alphanumeric, new string[] { SendToNumber }, message,
                    "BizSMS", "conBizsms");
                msgId = msgId.Trim();
            }
            catch (Exception ex)
            {
                msgId = "-1";
                logger.SetControllerAction("Helpers:SendSMS", "Send(...)");
                logger.Error(ex.Message);
            }

            return msgId;
        }
    }
}

try
{
    db.Database.ExecuteSqlCommand("EXEC dbo.sp_InsertNumbers {0}", model.ContractID);
}
catch (SqlException ex)
{
    Logger logger = new Logger();
    logger.SetControllerAction("AdminManage:Controller/CreateClient", "EXEC dbo.sp_InsertNumbers On Client Creating");
    logger.Error(ex.Message);
}