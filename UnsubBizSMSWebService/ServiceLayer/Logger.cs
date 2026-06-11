using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public static class Logger
    {
        private static string ConStr = ConfigurationManager.ConnectionStrings["BizSMS"].ConnectionString;
        //Loguje parametre za odjavu za NotifySms/NotifyCall
        public static void LogIncomingParams(string userNumber, string clientID)
        {
            var message = "Unsub params arrived, userNumber: " + userNumber + ", clientID: " + clientID;
            LogMessage(message);
        }

        //Loguje uspesnu odjavu za NotifySms/NotifyCall
        public static void LogSuccessUnsub(string userNumber, string clientID)
        {
            var message = "Success: unsubscribed userNumber: " + userNumber + ", clientID: " + clientID;
            LogMessage(message);
        }
        public static void LogMessage(string message)
        {
            using (var conn = new SqlConnection(ConStr))
            using (var command = new SqlCommand("Log_Insert", conn))
            {
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@Log_Date", SqlDbType.DateTime).Value = DateTime.Now;
                command.Parameters.Add("@Log_Level", SqlDbType.VarChar).Value = "INFO";
                command.Parameters.Add("@Log_Source", SqlDbType.VarChar).Value = "UnsubBizSMSWebService.ServiceLayer.Notify...";
                command.Parameters.Add("@User", SqlDbType.VarChar).Value = "SmsOdjava/0800_Odjava";
                command.Parameters.Add("@Controller", SqlDbType.VarChar).Value = "UnsubBizSMSService.svc";
                command.Parameters.Add("@Action", SqlDbType.VarChar).Value = "UnsubscribeUser...()";
                command.Parameters.Add("@Log_Message", SqlDbType.VarChar).Value = message;
                command.Parameters.Add("@Exception", SqlDbType.VarChar).Value = "";

                conn.Open();
                command.ExecuteNonQuery();
            };
        }
    }


}