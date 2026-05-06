using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using UnsubBizSMSWebService.SendSMShttp2sms;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public class SmsMessageSender : SendSmsProcesor
    {
        private static string ConStr = ConfigurationManager.ConnectionStrings["BizSMS"].ConnectionString;
        public SmsMessageSender()
        {
            Username = ConfigurationManager.AppSettings["sdpUsername"];
            Password = ConfigurationManager.AppSettings["sdpPassword"];
            SenderName = ConfigurationManager.AppSettings["Sender"];
            ReceiptRequest = null;
            Charging = null;
        }

        public void SendMessage(string msisdn, string message)
        {
            base.Msisdn = new[] { msisdn };
            base.MsgText = message;

            try
            {
                var result = SendSMS();
            }
            catch (Exception ex)
            {
                using (var conn = new SqlConnection(ConStr))
                using (var command = new SqlCommand("Log_Insert", conn))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    DateTime logDate = DateTime.Now;
                    string logLevel = "ERROR";
                    string logSource = "UnsubBizSMSWebService.ServiceLayer.SmsMessageSe";
                    string logUser = "SmsOdjava";
                    string logAction = "SendMessage";
                    Exception logEx = ex;

                    command.Parameters.Add("@Log_Date", SqlDbType.DateTime).Value = logDate;
                    command.Parameters.Add("@Log_Level", SqlDbType.VarChar).Value = logLevel;
                    command.Parameters.Add("@Log_Source", SqlDbType.VarChar).Value = logSource;
                    command.Parameters.Add("@User", SqlDbType.VarChar).Value = logUser;
                    command.Parameters.Add("@Controller", SqlDbType.VarChar).Value = "";
                    command.Parameters.Add("@Action", SqlDbType.VarChar).Value = logAction;
                    command.Parameters.Add("@Log_Message", SqlDbType.VarChar).Value = "Nije moguce poslati povratnu poruku korisniku";
                    command.Parameters.Add("@Exception", SqlDbType.VarChar).Value = logEx.ToString();

                    conn.Open();
                    command.ExecuteNonQuery();
                };
            }
        }

        public string SendSmsToNonMts(string alphanumeric, string message, http2sms h2s, string msisdn)
        {
            string SendToNumber = msisdn;
            string msgId = "-1";

            try
            {
                msgId = h2s.Send(alphanumeric, new string[] { SendToNumber }, message,
                    "BizSMSUnsubscribe", "");
                msgId = msgId.Trim();
            }
            catch (Exception ex)
            {
                using (var conn = new SqlConnection(ConStr))
                using (var command = new SqlCommand("Log_Insert", conn))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    DateTime logDate = DateTime.Now;
                    string logLevel = "ERROR";
                    string logSource = "UnsubBizSMSWebService.ServiceLayer.SmsMessageSe";
                    string logUser = "SmsOdjava";
                    string logAction = "SendSmsToNonMts";
                    Exception logEx = ex;

                    command.Parameters.Add("@Log_Date", SqlDbType.DateTime).Value = logDate;
                    command.Parameters.Add("@Log_Level", SqlDbType.VarChar).Value = logLevel;
                    command.Parameters.Add("@Log_Source", SqlDbType.VarChar).Value = logSource;
                    command.Parameters.Add("@User", SqlDbType.VarChar).Value = logUser;
                    command.Parameters.Add("@Controller", SqlDbType.VarChar).Value = "";
                    command.Parameters.Add("@Action", SqlDbType.VarChar).Value = logAction;
                    command.Parameters.Add("@Log_Message", SqlDbType.VarChar).Value = "Nije moguce poslati povratnu poruku korisniku";
                    command.Parameters.Add("@Exception", SqlDbType.VarChar).Value = logEx.ToString();

                    conn.Open();
                    command.ExecuteNonQuery();
                };
                msgId = "-1";
            }
            return msgId;
        }
    }
}